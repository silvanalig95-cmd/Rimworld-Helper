using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // Release E's touchpoints, continued (Peoples/PeoplesE.cs has the Stormglass Conclave and the Mossborn): the Ashen
    // Banner's squad hired for a season (a camp of theirs that takes up arms, Camps/CampContract.cs), its deserter and its
    // Roll, the Kiln Guild's commissions, its collection of them paid by quality and its bids on masterworks, and the two
    // asks that are guests staying a while: the Conclave's apprentice of the sky and the guild's wander-year apprentice.

    // ===================== The Ashen Banner: a squad for the season =====================

    /// <summary>
    /// A sergeant of the Ashen Banner comes to the gate with a squad of the company (three to five in all), offering to
    /// hold the colony's walls for a season. Hired, the first wage is carried out to the sergeant, and the squad makes camp
    /// by the colony for the season: a camp of theirs that takes up arms against whatever threatens the colony while it
    /// stands (CampKindDef.fights), as a war party does. Every five days the paymaster's due is asked by letter; paid, the
    /// silver is carried out to their fire; left unpaid, they strike camp and walk, and the Banner remembers it. Or the
    /// sergeant drills the colony's best fighter for an afternoon, no contract. {COUNT} is a wage, five days for the squad.
    /// </summary>
    public class HappeningWorker_BannerHire : HappeningWorker_Visitors
    {
        public static CampKindDef SquadKind => DefDatabase<CampKindDef>.GetNamedSilentFail("Rimfolk_Camp_BannerSquad");

        /// <summary>A camp can stand on the map now: camps on, none here, no danger, a spot, bearable weather.</summary>
        public static bool CanCamp(Map map) =>
            map != null && CampTracker.Enabled && SquadKind != null && CampTracker.VisitingAt(map) == null && !MapFacts.ThreatOn(map) && CampSite.AnyRoom(map)
            && CampWeather.Hazard(map) == null && !CampWeatherMath.TooExtremeToCome(map.mapTemperature.OutdoorTemp);

        private static bool Fighter(Pawn p) => p != null && !p.WorkTagIsDisabled(WorkTags.Violent) && p.skills?.GetSkill(SkillDefOf.Melee)?.TotallyDisabled == false;

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Mercenaries) != null && CanCamp(map) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mercenaries);
            if (ctx.faction == null) return false;
            int n = Rand.RangeInclusive(PeoplesEMath.SquadMin, PeoplesEMath.SquadMax);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_AshenBanner_Sergeant"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            for (int i = 1; i < n; i++)
            {
                var p = Arrivals.Make(PeoplesD.FighterKind(ctx.faction), ctx.faction, ext.ages);
                if (p != null) ctx.group.Add(p);
            }
            ctx.variant = ctx.group.Count + 1;
            ctx.tale = ctx.variant.ToString();
            ctx.count = PeoplesEMath.Wage(ctx.variant, Peoples.AgeOf(ctx.faction));
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.b = PeoplesD.Best(ctx.map, SkillDefOf.Melee, Fighter);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.b == null) tags.Add("nofighter");
            if (MapFacts.ThreatOn(ctx.map) || ctx.map.storyState != null && Find.TickManager.TicksGame - ctx.map.storyState.LastThreatBigTick < 5 * GenDate.TicksPerDay) tags.Add("raided");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option == 0 && !CanCamp(ctx.map) ? "CC_PeoplesE_NoCamp".Translate().Resolve()
            : option == 1 && (ctx.b == null || ctx.b.Dead || ctx.b.Downed) ? "CC_PeoplesE_NoFighter".Translate().Resolve()
            : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option == 0 ? "hired" : option == 1 ? "drill" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The squad hired: they make camp by the colony for the season, the sergeant at their head (BannerSquad.Start).</summary>
    public class DilemmaEffect_BannerHire : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => HappeningWorker_BannerHire.CanCamp(ctx?.map) ? null : "CC_PeoplesE_NoCamp".Translate().Resolve();

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            int n = (ctx as HappeningContext)?.variant ?? PeoplesEMath.SquadMin;
            sheet.Play("CC_PeoplesE_Impact_Hire".Translate(n, PeoplesEMath.HireDays, PeoplesEMath.WageDays, ctx?.count ?? 0).Resolve(), "CC_PeoplesE_Impact_HireBrief".Translate().Resolve());
            sheet.Add(ImpactKind.People, 1, "CC_PeoplesE_Impact_HireGuard".Translate().Resolve(), "CC_PeoplesE_Impact_HireGuardBrief".Translate().Resolve(), "bannersquad");
        }

        public override void Apply(DilemmaContext ctx) => BannerSquad.Start(ctx as HappeningContext);
    }

    /// <summary>The Banner's squad as a camp of theirs (Rimfolk_Camp_BannerSquad): made of the squad at the gate, and what is left unpaid.</summary>
    public static class BannerSquad
    {
        /// <summary>The squad at the gate makes camp for the season. False when no camp can stand now (they go home).</summary>
        public static bool Start(HappeningContext ctx)
        {
            var map = ctx?.map;
            var kind = HappeningWorker_BannerHire.SquadKind;
            var tracker = CampTracker.Instance;
            if (map == null || kind == null || tracker == null || ctx.faction == null) return false;
            var squad = ctx.group.Prepend(ctx.stranger).Where(p => PeoplesE.Up(p, map)).ToList();
            if (squad.Count == 0 || !HappeningWorker_BannerHire.CanCamp(map)) return false;
            try
            {
                var usual = CampWeather.StayRange(map, PeoplesEMath.HireDays * GenDate.TicksPerDay);
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(map, out var center, hot)) return false;
                center = CampLand.SiteFor(kind, map, center, hot);
                if (!center.IsValid) return false;
                var setup = tracker.Make(map, center, CampSite.ColonyCenter(map), kind, ctx.faction, hot, null, null, squad);
                if (setup == null) return false;
                setup.onMap = true;
                foreach (var p in squad) p.GetLord()?.RemovePawn(p);
                var camp = tracker.Start(setup);
                if (camp == null)
                {
                    Arrivals.SendAway(squad);
                    return false;
                }
                camp.Tell(camp.Format(Flavor.PickText(kind.titles), camp.leader, ctx.a), camp.Format(Flavor.PickText(kind.texts), camp.leader, ctx.a), LetterDefOf.PositiveEvent, true, camp.leader);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples E: the Banner's squad makes camp", e);
                return false;
            }
        }

        /// <summary>A wage for the squad as it stands now (those of them still living), by the age the Banner lives in.</summary>
        public static int WageFor(Camp c) => c == null ? 0 : PeoplesEMath.Wage(c.members.Count(p => p != null && !p.Dead), Peoples.AgeOf(c.faction));

        /// <summary>Left unpaid: they walk (the camp's ending letter, "unpaid", says so), and the Banner thinks less of the colony.</summary>
        public static void Unpaid(Camp c)
        {
            if (c?.faction == null) return;
            if (c.faction.HasGoodwill && !c.faction.IsPlayer) WorldSituationWorker.Goodwill(c.faction, PeoplesEMath.UnpaidGoodwill);
        }

        /// <summary>The contract kept to its end: the Banner thinks well of it.</summary>
        public static void Kept(Camp c)
        {
            if (c?.faction == null || !c.faction.HasGoodwill || c.faction.IsPlayer) return;
            WorldSituationWorker.Goodwill(c.faction, PeoplesEMath.ContractGoodwill);
        }
    }

    /// <summary>
    /// The Banner's wage paid (the camp's ask "wage", by letter every five days): the silver is the ask's own price
    /// (Camp.ContractWagePrice), carried out to their fire as the ask is answered; this marks the wage paid, so they stay.
    /// </summary>
    public class DilemmaEffect_BannerWage : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null) return;
            int k = PeoplesEMath.WageNumber(c.DaysHere);
            if (k > 0 && !c.asksMade.Contains(Camp.WagePaid + k)) c.asksMade.Add(Camp.WagePaid + k);
        }
    }

    // ===================== The Ashen Banner: a deserter =====================

    /// <summary>
    /// A deserter of the Ashen Banner comes to the gate at dusk, without the tabard, asking shelter: taken in, they are the
    /// colony's (and the Banner, who struck the name from the Roll, thinks a little less of the colony for it); a night's
    /// lodging in a warm corner, and on their way at first light; or the gate stays shut.
    /// </summary>
    public class HappeningWorker_BannerDeserter : HappeningWorker_Visitors
    {
        /// <summary>The Banner the deserter ran from: any people of its calling still standing, at peace or not.</summary>
        public static Faction Banner() =>
            Find.FactionManager.AllFactionsListForReading.Where(f => Peoples.CallingOf(f) == PeoplesMath.Mercenaries && !f.defeated && !f.IsPlayer && f.def.humanlikeFaction)
                .RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Banner() != null && PeoplesMath.ShelterHour(GenLocalDate.HourOfDay(map)) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Banner();
            if (ctx.faction == null) return false;
            ctx.stranger = Arrivals.Make(PeoplesD.FighterKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.xWaiting = false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).RandomElementWithFallback();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            // The tabard left behind on the road: they come without it.
            var tabard = ctx.stranger.apparel?.WornApparel.FirstOrDefault(a => a.def.defName == "VFEM2_Apparel_Tabard");
            if (tabard != null) ctx.stranger.apparel.Remove(tabard);
            tabard?.Destroy();
            return Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), PeoplesMath.HoursToLight(GenLocalDate.HourOfDay(ctx.map), 6)) != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.faction != null && ctx.faction.HostileTo(Faction.OfPlayer)) tags.Add("theirfoe");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option == 0 ? "taken" : option == 1 ? "night" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The Banner hears that the colony took in one of its deserters, and thinks a little less of it.</summary>
    public class DilemmaEffect_BannerDeserter : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var f = ctx?.faction;
            if (f != null && f.HasGoodwill && !f.IsPlayer) sheet.Goodwill(f, PeoplesEMath.DeserterTaken);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var f = ctx?.faction;
            if (f == null || !f.HasGoodwill || f.IsPlayer) return;
            WorldSituationWorker.Goodwill(f, PeoplesEMath.DeserterTaken);
            Tell.Message(NewsKind.World, Importance.Flavor, ctx.map, "CC_PeoplesE_DeserterHeard".Translate(f.Name, ctx.stranger?.LabelShort ?? "?").Resolve(), LookTargets.Invalid,
                MessageTypeDefOf.NeutralEvent);
        }
    }

    /// <summary>A night's lodging for the stranger, in a warm corner, on their way at first light (VisitorServices.Lodge).</summary>
    public class DilemmaEffect_NightLodging : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.stranger != null) sheet.Play("CC_PeoplesE_Impact_Night".Translate(ctx.stranger.LabelShort).Resolve(), "CC_PeoplesE_Impact_NightBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.stranger == null || ctx.map == null) return;
            if (!VisitorServices.Lodge(ctx.map, ctx.stranger, true)) VisitorServices.Stay(ctx.stranger, GenDate.TicksPerHour * 10);
        }
    }

    // ===================== The Ashen Banner: the Roll read at the gate =====================

    /// <summary>
    /// One of the colony fell within the month of their wounds and lies in a grave, and the keeper of the Roll of the Ashen
    /// Banner comes: the Roll keeps the name of everyone who fell holding a wall, of any people, and the keeper would read
    /// this one into it at the grave at dusk, with the colony gathered. Or the keeper and one of the colony at the grave a
    /// while. {DEAD} is the fallen.
    /// </summary>
    public class HappeningWorker_BannerRoll : HappeningWorker_Visitors
    {
        /// <summary>The grave of one of the colony who died of wounds within the Roll's days (the latest), or null.</summary>
        public static Building_Grave Fallen(Map map)
        {
            if (map == null) return null;
            int now = Find.TickManager.TicksGame;
            return HappeningWorker_Setting.Graves(map).Where(g => g.Corpse?.InnerPawn is Pawn d && PeoplesEMath.RollFor(now - g.Corpse.Age, now) && Wounded(d))
                .OrderBy(g => g.Corpse.Age).FirstOrDefault();
        }

        /// <summary>Died of wounds: an injury or a part shot away on them, and no illness that kills.</summary>
        private static bool Wounded(Pawn d) =>
            d.health?.hediffSet?.hediffs != null && d.health.hediffSet.hediffs.Any(h => h is Hediff_Injury || h is Hediff_MissingPart)
            && !d.health.hediffSet.hediffs.Any(h => h.def.lethalSeverity > 0f && h.def.makesSickThought);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Mercenaries) != null && Fallen(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Mercenaries);
            var grave = Fallen(ctx.map);
            if (ctx.faction == null || grave == null) return false;
            ctx.place = grave;
            var dead = grave.Corpse.InnerPawn;
            ctx.dead = dead?.LabelShort ?? "?";
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_AshenBanner_Paymaster"), ctx.faction, new FloatRange(40f, 65f));
            if (ctx.stranger == null) return false;
            ctx.a = PeoplesD.Able(ctx.map).OrderByDescending(p => dead != null && p.relations != null ? p.relations.OpinionOf(dead) : 0).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, ctx.place?.Position, ext.stayHours.RandomInRange) != null;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.place == null || ctx.place.Destroyed) ? "CC_PeoplesD_GraveGone".Translate(ctx.dead ?? "?").Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option <= 1 ? "roll" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The Kiln Guild: a commission =====================

    /// <summary>
    /// A journeyman of the Kiln Guild comes to commission a piece from the colony's best maker: a weapon or a garment of the
    /// colony's own benches (Crafting), or a sculpture (Artistic) where its best hand is an artist, made from the plainest
    /// material in the stores. Taken, the maker makes it at the bench; the journeyman comes back for it later and
    /// pays by its quality (Rimfolk_Evt_GuildCollects, PeoplesEMath.GuildPay). Or the journeyman watches the maker at work
    /// a while, for the guild's book. {THING} is the piece, {COUNT} what a well-made one fetches.
    /// </summary>
    public class HappeningWorker_GuildCommission : HappeningWorker_Callers
    {
        /// <summary>The colony's best maker for a commission: grown, up, and the better of its crafters and its artists.</summary>
        public static Pawn Maker(Map map, out SkillDef skill)
        {
            skill = null;
            Pawn Best(SkillDef s) => PeoplesD.Best(map, s, p => p.skills?.GetSkill(s)?.TotallyDisabled == false && !p.WorkTypeIsDisabled(WorkTypeDefOf.Hauling));
            var crafter = Best(SkillDefOf.Crafting);
            var artist = Best(SkillDefOf.Artistic);
            int c = crafter?.skills.GetSkill(SkillDefOf.Crafting).Level ?? -1, a = artist?.skills.GetSkill(SkillDefOf.Artistic).Level ?? -1;
            if (c < 0 && a < 0) return null;
            skill = a > c ? SkillDefOf.Artistic : SkillDefOf.Crafting;
            return a > c ? artist : crafter;
        }

        /// <summary>
        /// A piece the maker can make now: a thing of quality, made at a bench the colony has, from stuff alone, its recipe
        /// open to the colony, with enough of the plainest stuff for it in the stores (a sculpture for an artist).
        /// </summary>
        public static ThingDef Piece(Map map, SkillDef skill, out Thing stuff)
        {
            stuff = null;
            if (map == null || skill == null) return null;
            var pieces = new List<(ThingDef def, Thing stuff)>();
            foreach (var r in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                var def = r.ProducedThingDef;
                if (def == null || !def.MadeFromStuff || !def.HasComp(typeof(CompQuality)) || r.workSkill != skill || !r.AvailableNow) continue;
                if (!def.CostList.NullOrEmpty() || def.costStuffCount <= 0 || def.costStuffCount > 200) continue;
                if (skill == SkillDefOf.Crafting && !def.IsWeapon && !def.IsApparel) continue;
                if (skill == SkillDefOf.Artistic && !def.IsArt) continue;
                if (!DilemmaEffect_ArtCommission.Bench(map, def)) continue;
                var s = DilemmaEffect_ArtCommission.Stuff(map, def);
                if (s != null) pieces.Add((def, s));
            }
            if (pieces.Count == 0) return null;
            var pick = pieces.RandomElementByWeight(p => p.def.IsArt ? 1f : 2f);
            stuff = pick.stuff;
            return pick.def;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Artisans) != null && Colonists(map).Count > 0 && Maker(map, out var s) != null && Piece(map, s, out _) != null
            && (Callers.Instance?.commissions.Count ?? 0) < Callers.MaxCommissions;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Artisans);
            ctx.b = Maker(ctx.map, out var skill);
            if (ctx.faction == null || ctx.b == null) return false;
            ctx.thing = Piece(ctx.map, skill, out var stuff);
            if (ctx.thing == null || stuff == null) return false;
            ctx.skill = skill;
            ctx.note = stuff.def.defName;
            ctx.count = PeoplesEMath.GuildPay(StatDefOf.MarketValue.Worker.GetValueAbstract(ctx.thing, stuff.def), 3);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_KilnGuild_Journeyman"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.a = ctx.b;
            ctx.tale = ctx.thing.label;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, Gate(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.skill == SkillDefOf.Artistic) tags.Add("art");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option <= 1 ? "commission" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>
    /// The guild's commission set going: the maker (B) makes the piece (the event's thing) at the bench from the stuff set
    /// apart beside the stack in the stores, as the art collector's commission is made (PlayMake.KeepMade); the guild's
    /// journeyman comes back for it (Callers' commissions, kept for the guild).
    /// </summary>
    public class DilemmaEffect_GuildCommission : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static ThingDef StuffOf(DilemmaContext ctx) => (ctx as HappeningContext)?.note is string n ? DefDatabase<ThingDef>.GetNamedSilentFail(n) : null;

        private static Thing Stack(DilemmaContext ctx)
        {
            var def = ctx?.thing;
            var stuff = StuffOf(ctx);
            if (ctx?.map == null || def == null || stuff == null) return null;
            int need = Mathf.Max(1, def.CostStuffCount);
            return ctx.map.listerThings.ThingsOfDef(stuff).Where(t => t.Spawned && t.stackCount >= need && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer)).FirstOrDefault();
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx?.b == null || ctx.b.Dead || ctx.b.Downed) return "CC_PeoplesE_NoMaker".Translate(ctx?.b?.LabelShort ?? "?").Resolve();
            if (ctx.thing == null || !DilemmaEffect_ArtCommission.Bench(ctx.map, ctx.thing)) return "CC_PeoplesE_NoBench".Translate(ctx?.thing?.label ?? "?").Resolve();
            if (Stack(ctx) == null) return "CC_Dilemma_NotEnough".Translate(ctx.thing.CostStuffCount, StuffOf(ctx)?.label ?? "?").Resolve();
            return null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var stuff = StuffOf(ctx);
            if (ctx?.thing == null || ctx.b == null || stuff == null || ctx.stranger == null) return;
            sheet.Play("CC_PeoplesE_Impact_Commission".Translate(ctx.b.Named("PAWN"), ctx.thing.label.Named("THING"), ctx.stranger.Named("OTHER")).Resolve(),
                "CC_Impact_Caller_CommissionBrief".Translate().Resolve());
            sheet.Goods(-1, stuff, ctx.thing.CostStuffCount);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var def = ctx?.thing;
            var map = ctx?.map;
            var maker = ctx?.b;
            var x = ctx?.stranger;
            var stack = Stack(ctx);
            if (def == null || map == null || maker == null || x == null || stack == null) return;
            try
            {
                int need = Mathf.Max(1, def.CostStuffCount);
                Thing from = stack;
                if (stack.stackCount > need)
                {
                    from = stack.SplitOff(need);
                    var cell = GenAdj.CellsAdjacent8Way(stack).Where(c => c.InBounds(map) && c.Standable(map) && c.GetFirstItem(map) == null).FirstOrDefault(c => c.IsValid);
                    GenSpawn.Spawn(from, cell.IsValid && cell != default ? cell : stack.Position, map);
                }
                from.SetForbidden(true, false);
                var play = PlayMake.KeepMade(map, maker, def, from.def.defName, from, -1, 1, "CC_PeoplesE_Commission_Label".Translate(x.Named("OTHER")).Resolve(), ctx.skill ?? SkillDefOf.Crafting, null);
                if (play == null)
                {
                    from.SetForbidden(false, false);
                    if (PlayOuts.On) return;
                    var made = DilemmaEffect_Made.Make(def, from.def.defName, QualityUtility.GenerateQualityCreatedByPawn(maker, ctx.skill ?? SkillDefOf.Crafting), maker, ctx.skill ?? SkillDefOf.Crafting);
                    if (made == null) return;
                    var at = maker.Spawned && maker.Map == map ? maker.Position : from.Position;
                    from.Destroy();
                    if (!GenPlace.TryPlaceThing(made.def.Minifiable ? made.MakeMinified() : made, at, map, ThingPlaceMode.Near, out var lying)) return;
                    var owed = Callers.Commission(x, maker, def, -1, map);
                    if (owed != null)
                    {
                        owed.made = (lying as MinifiedThing)?.InnerThing ?? lying;
                        owed.madeTick = Find.TickManager.TicksGame;
                    }
                    return;
                }
                play.makeTicks = PlayOutMath.DurationTicks(CallerMath.CommissionHours);
                PlayOuts.Register(play);
                Callers.Commission(x, maker, def, play.id, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples E: the guild's commission", e);
            }
        }
    }

    /// <summary>
    /// The Kiln Guild's journeyman comes back for a commission the colony's maker finished (Callers.Due, the guild's
    /// commissions only), looks it over and pays by its quality (PeoplesEMath.GuildPay: more the better it is). Sold, it is
    /// carried out to them; kept, the guild marks it in their book and goes; or kept, and the gate shut on them.
    /// </summary>
    public class HappeningWorker_GuildCollects : HappeningWorker_Callers
    {
        public static bool Guilds(Commission c) => c?.faction != null && Peoples.CallingOf(c.faction) == PeoplesMath.Artisans;

        protected override bool CanHappen(Map map, HappeningExtension ext) => Colonists(map).Count > 0 && Callers.Due(map, Guilds) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var c = Callers.Due(ctx.map, Guilds);
            if (c == null) return false;
            ctx.stranger = c.collector;
            ctx.faction = c.faction;
            ctx.group.Clear();
            ctx.place = Callers.Outer(c.made);
            ctx.thing = c.def;
            ctx.variant = c.id;
            c.made.TryGetQuality(out var q);
            ctx.tale = q.GetLabel();
            ctx.count = PeoplesEMath.GuildPay(StatDefOf.MarketValue.Worker.GetValueAbstract(c.def, c.made.Stuff), (int)q);
            var maker = c.maker;
            ctx.a = maker != null && !maker.Dead && maker.IsColonist && maker.Spawned && maker.Map == ctx.map ? maker
                : Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            ctx.note = maker == ctx.a ? "maker" : null;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, Gate(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void Abort(HappeningContext ctx)
        {
            // Theirs to collect: a journeyman who couldn't come is still owed it.
            ctx.stranger = null;
            ctx.group.Clear();
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.note == "maker") tags.Add("maker");
            if (ctx.place != null && ((ctx.place as MinifiedThing)?.InnerThing ?? ctx.place).TryGetQuality(out var q) && q >= QualityCategory.Masterwork) tags.Add("masterwork");
        }
    }

    // ===================== The Kiln Guild: a bid on a masterwork =====================

    /// <summary>
    /// The colony has a masterwork or a legend of its own (a weapon, a garment, a sculpture, a piece of furniture, standing
    /// in its rooms or packed in its stores), and the Kiln Guild's master-trader comes to bid on it, well over its worth
    /// (PeoplesEMath.Bid). Sold, it is carried out to them; kept, word of it goes with them; or they are sent on.
    /// </summary>
    public class HappeningWorker_GuildBid : HappeningWorker_Callers
    {
        private static Thing Inner(Thing t) => (t as MinifiedThing)?.InnerThing ?? t;

        private static bool Fine(Thing t)
        {
            var inner = Inner(t);
            return inner?.def != null && inner.TryGetQuality(out var q) && (int)q >= PeoplesEMath.BidQuality && !inner.def.IsCorpse;
        }

        /// <summary>The colony's finest masterwork, by its worth: art and furniture standing in its rooms, anything packed or lying in its stores; or null.</summary>
        public static Thing Masterwork(Map map)
        {
            if (map == null) return null;
            var all = new List<Thing>();
            foreach (var b in map.listerBuildings.allBuildingsColonist)
                if (b.def.Minifiable && Fine(b) && !b.IsBurning()) all.Add(b);
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))
                if (t.Spawned && Fine(t) && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer)) all.Add(t);
            return all.OrderByDescending(t => Inner(t).MarketValue).FirstOrDefault();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Artisans) != null && Colonists(map).Count > 0 && Masterwork(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Artisans);
            var piece = Masterwork(ctx.map);
            if (ctx.faction == null || piece == null) return false;
            var inner = Inner(piece);
            inner.TryGetQuality(out var q);
            ctx.place = piece;
            ctx.thing = inner.def;
            ctx.tale = inner.TryGetComp<CompArt>()?.Title ?? inner.LabelNoCount;
            ctx.count = PeoplesEMath.Bid(inner.MarketValue, (int)q);
            ctx.stranger = Arrivals.Make(PeoplesD.TraderKind(ctx.faction), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            var maker = MakerOf(piece, ctx.map);
            ctx.a = maker != null && !maker.Downed ? maker : Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            ctx.note = maker == ctx.a && maker != null ? "maker" : null;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            ctx.variant = (int)q;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) == null) return false;
            // A look at the piece before the bid, where it stands or lies.
            Callers.View(ctx.map, ctx.stranger, new List<Thing> { ctx.place }, def.defName);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.note == "maker") tags.Add("maker");
            if (ctx.variant >= 6) tags.Add("legendary");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesD.Say(ctx.stranger, Ext, ctx, option == 0 ? "bought" : option == 1 ? "kept" : "refused");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The asks that stay a while =====================

    /// <summary>
    /// The Stormglass Conclave asks the colony to host an apprentice of the glass for some days, to study its sky: they
    /// come a couple of days after the colony agrees, eat at its table and sleep in a bed it has free (Hosting), and each
    /// clear night they lie out under the open sky near the gate a couple of hours, watching it (PeoplesE.SkyJob). At the end they
    /// leave the Conclave's thanks in silver, and the colony's weather-reader, who sat up with them, is the better at the
    /// fields' weather for it. Nights watched are counted; none at all and the stay is a failure.
    /// </summary>
    public class HappeningWorker_MissionGlassApprentice : HappeningWorker_MissionHost
    {
        protected override string Kind => "glassApprentice";

        protected override float RestDays => 75f;

        protected override bool Possible(Map map) => PeoplesD.Friendly(PeoplesMath.Seers) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = PeoplesD.Friendly(PeoplesMath.Seers);
            if (m.faction == null) return false;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = PeoplesD.Best(map, SkillDefOf.Plants) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            m.x = Arrivals.Make(PeoplesD.KindNamed(m.faction, "Rimfolk_Stormglass_Apprentice"), m.faction, new FloatRange(16f, 24f));
            if (m.x == null) return false;
            Arrivals.Hold(m.x);
            m.name = m.x.LabelShort;
            m.wait = Rand.Range(1.5f, 2.5f);
            m.days = m.wait + Rand.Range(PeoplesEMath.ApprenticeMinDays, PeoplesEMath.ApprenticeMaxDays);
            m.reward = StoryChains.Silver(map, 120f, 260f);
            m.goodwill = 10;
            m.penalty = 5;
            Fill(m, ctx);
            ctx.stranger = m.x;
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage >= 1 && Harmed(m))
            {
                Missions.Goodwill(m, -m.penalty * 2);
                Missions.Finish(m, MissionState.Failed, "harmed", null);
                return;
            }
            if (m.stage == 0)
            {
                Remind(m, map, now);
                if (now < m.due) return;
                if (!Arrive(m, map, PeoplesD.GateSpot(map), (m.deadline - now) / (float)GenDate.TicksPerHour))
                {
                    Missions.Finish(m, MissionState.Failed, "ended", null);
                    return;
                }
                m.stage = 1;
                m.lastLook = now;
                string line = Line(More?.ready, m, map);
                if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(x), MessageTypeDefOf.PositiveEvent);
                return;
            }
            Tend(m, map);
            // Each night, the sky watched from the open by the gate: a night counted once.
            int hour = GenLocalDate.HourOfDay(map);
            int night = (now + 6 * GenDate.TicksPerHour) / GenDate.TicksPerDay;
            if ((hour >= 21 || hour <= 2) && x.Spawned && x.Map == map && Hosting.Free(x) && map.weatherManager.curWeather?.rainRate <= 0.1f && m.misses != night)
            {
                var spot = PeoplesD.GateSpot(map);
                if (spot.HasValue && RCellFinder.TryFindRandomCellNearWith(spot.Value, c => c.Standable(map) && !c.Roofed(map) && c.GetFirstBuilding(map) == null, map, out var cell, 4, 12)
                    && PeoplesE.Start(x, PeoplesE.SkyJob, cell, null, PeoplesEMath.SkyHours * GenDate.TicksPerHour))
                    m.misses = night;
            }
            bool gone = !x.Spawned || x.Map != map;
            if (now < m.deadline && !gone) return;
            var look = x.Spawned ? new LookTargets(x) : LookTargets.Invalid;
            if (m.tally == 0)
            {
                SeeOff(m, map);
                Missions.Finish(m, MissionState.Failed, "cloudy", null, look);
                return;
            }
            Couriers.PaySilver(map, x.Spawned && x.Map == map ? x.Position : IntVec3.Invalid, m.reward, m.faction, x);
            Missions.Goodwill(m, m.goodwill);
            if (m.a != null && !m.a.Dead) m.a.skills?.Learn(SkillDefOf.Plants, 400f * Mathf.Min(6, m.tally), true);
            SeeOff(m, map);
            Missions.Finish(m, MissionState.Done, "watched", m.reward.ToString(), look);
        }

        /// <summary>A night's watch done under the open sky: the night counted for the stay, and said over their head.</summary>
        public static void Watched(Pawn pawn)
        {
            var m = StoriesComponent.Instance?.missions?.FirstOrDefault(x => x.Open && x.kind == "glassApprentice" && x.x == pawn);
            if (m == null) return;
            m.tally++;
            Speech.Say(pawn, "CC_PeoplesE_ApprenticeSays".Translate().Resolve());
        }

        protected override Thing FoodFor(Mission m, Map map, Pawn p) => Hosting.Meal(p, false);

        protected override Building_Bed BedFor(Mission m, Map map, Pawn p) =>
            Hosting.Beds(map, o => o == p, p).OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();

        public override string Status(Mission m) =>
            m.stage == 0
                ? "CC_PeoplesE_Apprentice_Coming".Translate(m.name ?? "?").Resolve()
                : "CC_PeoplesE_Apprentice_Staying".Translate(m.name ?? "?", m.tally).Resolve() + " " + MealsText(m);
    }

    /// <summary>
    /// The Kiln Guild asks the colony to take a journeyman on their wander-year for some days: they come a couple of days
    /// after the colony agrees, eat at its table and sleep in a bed it has free (Hosting), and each working day they work
    /// two hours at the bench nearest the colony's best maker, learning (JobDriver_PeoplesE at the bench). At the end they
    /// leave a piece of guild work of their own making by the gate, marked (a sculpture of good quality), and the guild's
    /// thanks; the days at the bench are counted, and none at all is a failure.
    /// </summary>
    public class HappeningWorker_MissionWanderYear : HappeningWorker_MissionHost
    {
        protected override string Kind => "wanderYear";

        protected override float RestDays => 75f;

        protected override bool Possible(Map map) => PeoplesD.Friendly(PeoplesMath.Artisans) != null && WanderYear.Bench(map, null) != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            m.faction = PeoplesD.Friendly(PeoplesMath.Artisans);
            if (m.faction == null) return false;
            m.place = LivingEvents.TownOf(m.faction, map) ?? m.faction.Name;
            m.a = HappeningWorker_GuildCommission.Maker(map, out _) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            m.x = Arrivals.Make(PeoplesD.KindNamed(m.faction, "Rimfolk_KilnGuild_Apprentice"), m.faction, new FloatRange(16f, 24f));
            if (m.x == null) return false;
            Arrivals.Hold(m.x);
            m.name = m.x.LabelShort;
            m.wait = Rand.Range(1.5f, 2.5f);
            m.days = m.wait + Rand.Range(PeoplesEMath.WanderMinDays, PeoplesEMath.WanderMaxDays);
            m.reward = StoryChains.Silver(map, 80f, 180f);
            m.goodwill = 10;
            m.penalty = 5;
            Fill(m, ctx);
            ctx.stranger = m.x;
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var x = m.x;
            if (x == null || x.Discarded)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            if (m.stage >= 1 && Harmed(m))
            {
                Missions.Goodwill(m, -m.penalty * 2);
                Missions.Finish(m, MissionState.Failed, "harmed", null);
                return;
            }
            if (m.stage == 0)
            {
                Remind(m, map, now);
                if (now < m.due) return;
                if (!Arrive(m, map, PeoplesD.GateSpot(map), (m.deadline - now) / (float)GenDate.TicksPerHour))
                {
                    Missions.Finish(m, MissionState.Failed, "ended", null);
                    return;
                }
                m.stage = 1;
                string line = Line(More?.ready, m, map);
                if (!line.NullOrEmpty()) Tell.Message(NewsKind.World, Importance.Important, map, line, new LookTargets(x), MessageTypeDefOf.PositiveEvent);
                return;
            }
            Tend(m, map);
            // A working day's two hours at the bench nearest the colony's maker: a day counted once.
            int hour = GenLocalDate.HourOfDay(map);
            int day = now / GenDate.TicksPerDay;
            if (hour >= 9 && hour <= 16 && x.Spawned && x.Map == map && Hosting.Free(x) && m.misses != day)
            {
                var bench = WanderYear.Bench(map, m.a);
                if (bench != null && PeoplesE.Start(x, PeoplesE.BenchJob, bench, null, PeoplesEMath.BenchHours * GenDate.TicksPerHour)) m.misses = day;
            }
            bool gone = !x.Spawned || x.Map != map;
            if (now < m.deadline && !gone) return;
            var look = x.Spawned ? new LookTargets(x) : LookTargets.Invalid;
            if (m.tally == 0)
            {
                SeeOff(m, map);
                Missions.Finish(m, MissionState.Failed, "idle", null, look);
                return;
            }
            var piece = WanderYear.LeavePiece(x, map);
            Couriers.PaySilver(map, x.Spawned && x.Map == map ? x.Position : IntVec3.Invalid, m.reward, m.faction, x);
            Missions.Goodwill(m, m.goodwill);
            SeeOff(m, map);
            string left = ((piece as MinifiedThing)?.InnerThing ?? piece)?.LabelNoCount;
            Missions.Finish(m, MissionState.Done, "worked", left ?? "CC_PeoplesE_Wander_NoPiece".Translate().Resolve(), piece != null ? new LookTargets(piece) : look);
        }

        protected override Thing FoodFor(Mission m, Map map, Pawn p) => Hosting.Meal(p, false);

        protected override Building_Bed BedFor(Mission m, Map map, Pawn p) =>
            Hosting.Beds(map, o => o == p, p).OrderBy(b => b.Position.DistanceToSquared(p.Position)).FirstOrDefault();

        public override string Status(Mission m) =>
            m.stage == 0
                ? "CC_PeoplesE_Wander_Coming".Translate(m.name ?? "?").Resolve()
                : "CC_PeoplesE_Wander_Staying".Translate(m.name ?? "?", m.tally).Resolve() + " " + MealsText(m);
    }

    /// <summary>The wander-year's work at the colony's benches, and the piece the journeyman leaves.</summary>
    public static class WanderYear
    {
        /// <summary>A work bench of the colony's where things of quality are made: the one nearest the maker.</summary>
        public static Thing Bench(Map map, Pawn maker)
        {
            if (map == null) return null;
            var at = maker != null && maker.Spawned && maker.Map == map ? maker.Position : CampSite.ColonyCenter(map);
            return map.listerBuildings.allBuildingsColonist.Where(b => b is Building_WorkTable w && w.def.AllRecipes.Any(r => r.workSkill == SkillDefOf.Crafting || r.workSkill == SkillDefOf.Artistic)
                                                                       && !b.IsForbidden(Faction.OfPlayer) && b.def.hasInteractionCell)
                .OrderBy(b => at.IsValid ? b.Position.DistanceToSquared(at) : 0).FirstOrDefault();
        }

        /// <summary>A morning at the bench done: the day counted for the stay, the journeyman the better for it, the maker who showed them a little too.</summary>
        public static void Worked(Pawn pawn, Thing bench)
        {
            var m = StoriesComponent.Instance?.missions?.FirstOrDefault(x => x.Open && x.kind == "wanderYear" && x.x == pawn);
            if (m == null) return;
            m.tally++;
            pawn.skills?.Learn(SkillDefOf.Crafting, 600f, true);
            if (m.a != null && m.a.Spawned && bench != null && m.a.Position.InHorDistOf(bench.Position, 12f)) m.a.skills?.Learn(SkillDefOf.Crafting, 250f);
            Speech.Say(pawn, "CC_PeoplesE_WanderSays".Translate().Resolve());
        }

        /// <summary>
        /// The journeyman's piece left by the gate: a small work of art of the guild's age (Things/ThingRoles.cs, SmallArt: a
        /// small sculpture, or a mod's statue or pottery), of stone or wood where it takes a material, good, their mark on it.
        /// </summary>
        public static Thing LeavePiece(Pawn x, Map map)
        {
            if (x == null || map == null) return null;
            var stuffs = new[] { "BlocksMarble", "BlocksGranite", "WoodLog" }.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(s => s?.stuffProps != null).ToList();
            bool Takes(ThingDef d) => !d.MadeFromStuff || stuffs.Any(s => s.stuffProps.CanMake(d));
            var def = ThingRoles.From(ThingRole.SmallArt, x.Faction, map, 0, d => d.Minifiable && Takes(d));
            if (def == null) return null;
            string stuff = def.MadeFromStuff ? stuffs.FirstOrDefault(s => s.stuffProps.CanMake(def))?.defName : null;
            // Guild work, good: the quality the guild sends its journeymen out with, whoever's hands it was.
            var made = DilemmaEffect_Made.Make(def, stuff, (QualityCategory)PeoplesEMath.LeftQuality, x, null);
            if (made == null) return null;
            var gate = PeoplesD.GateSpot(map) ?? (x.Spawned ? x.Position : CampSite.ColonyCenter(map));
            var packed = made.def.Minifiable ? made.MakeMinified() : made;
            return GenPlace.TryPlaceThing(packed, gate, map, ThingPlaceMode.Near, out var lying) ? lying : null;
        }
    }
}
