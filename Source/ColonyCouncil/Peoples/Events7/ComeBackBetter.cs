using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    // ===================== 61. Come back better (the Kiln Guild) =====================

    /// <summary>
    /// Come back better (item 61): a year (sixty days) after a wander-year at the colony ended well (the mission's own end,
    /// World/Patch_FactionMemory.cs, where the guild remembers it as one of theirs who learned at the colony: "learnedHere"), the same
    /// journeyman comes back to the gate a master of the guild, their craft grown to a master's, with a masterwork of their own making
    /// as a gift for the colony. The maker who taught them at the bench gets a lasting memory of it. The journeyman is kept in the
    /// world by the ledger until then (KeepReason). If the guild is at war with the colony when it is due, the visit waits a season.
    /// </summary>
    public static class ComeBackBetter
    {
        public const string EventDef = "Rimfolk_Evt_ComeBackBetter";

        /// <summary>Why the ledger keeps the journeyman in the world until they come back (World/Ledger/LedgerKeep.cs).</summary>
        public const string KeepReason = "comeBack";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The record being fired for (set only while it is).</summary>
        private static ComeBackRecord firing;

        /// <summary>A record waits on them.</summary>
        public static bool Holds(Pawn p) => p != null && (PeopleEvents7Book.Instance?.comeBacks.Any(r => r != null && !r.done && r.pawn == p) ?? false);

        /// <summary>A wander-year at the colony ended well (Patch_FactionMemory.MissionEnded): the journeyman noted for a year on, and kept.</summary>
        public static void Ended(Mission m)
        {
            var b = PeopleEvents7Book.Instance;
            var x = m?.x;
            if (b == null || x == null || x.Dead || m.faction == null || m.a == null) return;
            var map = Find.Maps.FirstOrDefault(mp => mp.uniqueID == m.mapId) ?? m.a.MapHeld;
            if (map == null || !Homes.IsHome(map)) map = Homes.All.FirstOrDefault();
            b.AddComeBack(new ComeBackRecord
            {
                pawn = x, maker = m.a, faction = m.faction, map = map, name = x.LabelShort, ended = Now, due = Now + PeopleEvents7Math.ComeBackDays * GenDate.TicksPerDay
            });
            WorldLedger.Keep(x, KeepReason);
        }

        private static void Close(ComeBackRecord r)
        {
            r.done = true;
            if (r.pawn != null && WorldLedger.Release(r.pawn, KeepReason)) Arrivals.Release(r.pawn);
        }

        /// <summary>The maker still the colony's, at a home.</summary>
        private static bool MakerHome(ComeBackRecord r) => r.maker != null && !r.maker.Dead && r.maker.Faction == Faction.OfPlayer && r.maker.Spawned && Homes.IsHome(r.maker.Map);

        /// <summary>The journeyman can come: alive, out in the world, still the guild's.</summary>
        private static bool Free(ComeBackRecord r) =>
            r.pawn != null && !r.pawn.Dead && !r.pawn.Discarded && !r.pawn.Spawned && r.pawn.Faction == r.faction && r.pawn.GetCaravan() == null && !People.Kidnapped(r.pawn)
            && !r.pawn.IsPrisoner;

        /// <summary>
        /// The events' hour: a journeyman due back comes to the maker's home (the guild at peace, a question may be asked, no threat
        /// on); one who can no longer come is let go; the guild at war puts the visit off a season.
        /// </summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents7Book.Instance;
            if (b == null) return;
            foreach (var r in b.comeBacks.Where(r => r != null && !r.done).ToList())
            {
                if (r.pawn == null || r.pawn.Dead || r.pawn.Discarded || r.faction == null || r.faction.defeated || r.maker == null || r.maker.Dead
                    || now - r.ended > PeopleEvents7Math.ComeBackForgetDays * GenDate.TicksPerDay)
                {
                    Close(r);
                    continue;
                }
                if (now < r.due || !Free(r) || !MakerHome(r) || !Tell.CanAsk || MapFacts.ThreatOn(r.maker.Map)) continue;
                if (!PeoplesD.Theirs(r.faction, PeoplesMath.Artisans) || r.faction.HostileTo(Faction.OfPlayer))
                {
                    r.due = now + PeopleEvents7Math.ComeBackRetryDays * GenDate.TicksPerDay;
                    continue;
                }
                if (Fire(r)) Close(r);
                else r.due = now + GenDate.TicksPerDay;
            }
        }

        private static bool Fire(ComeBackRecord r)
        {
            firing = r;
            try
            {
                return PeopleEvents2.Fire(EventDef, r.maker.Map, r.faction);
            }
            finally
            {
                firing = null;
            }
        }

        /// <summary>The record the event is for now (fired by the watch, or the first one due at this home).</summary>
        public static ComeBackRecord For(Map map) =>
            firing ?? PeopleEvents7Book.Instance?.comeBacks.FirstOrDefault(r => r != null && !r.done && Free(r) && MakerHome(r) && r.maker.Map == map && Now >= r.due);

        /// <summary>Their gift: a work of the guild's own age, of stone or wood where it takes a material, a masterwork, their name on it.</summary>
        public static ThingDef GiftDef(Faction f, Map map, out string stuff)
        {
            stuff = null;
            var stuffs = new[] { "BlocksMarble", "BlocksGranite", "WoodLog" }.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(s => s?.stuffProps != null).ToList();
            bool Takes(ThingDef d) => !d.MadeFromStuff || stuffs.Any(s => s.stuffProps.CanMake(d));
            var def = ThingRoles.From(ThingRole.SmallArt, f, map, 0, d => d.Minifiable && Takes(d));
            if (def != null && def.MadeFromStuff) stuff = stuffs.FirstOrDefault(s => s.stuffProps.CanMake(def))?.defName;
            return def;
        }

        /// <summary>The gift made and set down at the gate by the master, the maker's memory, the guild's thanks.</summary>
        public static void Given(HappeningContext ctx)
        {
            var map = ctx?.map;
            var x = ctx?.stranger;
            if (map == null || x == null) return;
            if (ctx.thing != null)
                try
                {
                    var made = DilemmaEffect_Made.Make(ctx.thing, ctx.note, QualityCategory.Masterwork, x, null);
                    if (made != null)
                    {
                        var packed = made.def.Minifiable ? made.MakeMinified() : made;
                        var near = x.Spawned && x.Map == map ? x.Position : PeoplesD.GateSpot(map) ?? CampSite.ColonyCenter(map);
                        Couriers.Loot(new List<Thing> { packed }, map, near, ctx.faction, x.Spawned ? x : null);
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 7: the master's gift", e);
                }
            PeopleEvents7.Thought(ctx.a, "Rimfolk_Thought_ApprenticeMaster");
            PeopleEvents7.Goodwill(ctx.faction, PeopleEvents7Math.ComeBackGoodwill, GoodwillWhy.Gift);
        }

        /// <summary>Dev mode: a journeyman of the guild due back now (one made for a wander-year at the colony's best maker, if none is on record).</summary>
        public static string Soon(Map map)
        {
            var b = PeopleEvents7Book.Instance;
            var f = PeoplesD.Friendly(PeoplesMath.Artisans);
            if (b == null || map == null || f == null) return "no Kiln Guild at peace.";
            var r = b.comeBacks.FirstOrDefault(x => x != null && !x.done);
            if (r == null)
            {
                var maker = PeoplesD.Best(map, SkillDefOf.Crafting);
                var x = Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_KilnGuild_Apprentice"), f, new FloatRange(17f, 25f));
                if (maker == null || x == null) return "no maker here, or no journeyman could be made.";
                Arrivals.Hold(x);
                r = new ComeBackRecord { pawn = x, maker = maker, faction = f, map = map, name = x.LabelShort, ended = Now - PeopleEvents7Math.ComeBackDays * GenDate.TicksPerDay };
                b.AddComeBack(r);
                WorldLedger.Keep(x, KeepReason);
            }
            r.due = Now;
            return r.name + " comes back within the hour (when a question may be asked).";
        }
    }

    /// <summary>
    /// {X}, the journeyman who spent their wander-year at {A}'s bench a year ago, back at the gate a master of {FACTION} with a {TALE}
    /// of their own making as a gift for the colony.
    /// </summary>
    public class HappeningWorker_ComeBackBetter : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Artisans) != null && ComeBackBetter.For(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var r = ComeBackBetter.For(ctx.map);
            if (r?.pawn == null || r.maker == null) return false;
            ctx.faction = r.faction;
            ctx.stranger = r.pawn;
            ctx.a = r.maker;
            ctx.thing = ComeBackBetter.GiftDef(ctx.faction, ctx.map, out var stuff);
            ctx.note = stuff;
            var stuffDef = stuff.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(stuff);
            ctx.tale = ctx.thing == null ? "" : stuffDef != null ? "CC_PEv7_GiftOf".Translate(stuffDef.LabelAsStuff, ctx.thing.label).Resolve() : ctx.thing.label;
            // A year at the guild's benches: a master's hand now.
            foreach (var sk in new[] { SkillDefOf.Crafting, SkillDefOf.Artistic })
                StorytellerEvents.Skilled(ctx.stranger, sk, new IntRange(PeopleEvents7Math.MasterSkill, PeopleEvents7Math.MasterSkill + 3));
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.thing != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.a != null && (ctx.a.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 0) < (ctx.stranger?.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 0)) tags.Add("better");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Map != ctx.map)) return "CC_PEv7_MakerAway".Translate(ctx.a?.LabelShort ?? "?").Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "sit" : option == 1 ? "bench" : "gate");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The master's gift laid down by them at the gate, the maker's lasting memory, the guild's thanks (ComeBackBetter.Given).</summary>
    public class DilemmaEffect_ComeBackBetter : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.stranger == null) return;
            if (hc.thing != null) sheet.Add(ImpactKind.Goods, 1, "CC_PEv7_Impact_Gift".Translate(hc.stranger.LabelShort, hc.tale ?? "?").Resolve(), "CC_PEv7_Impact_GiftBrief".Translate().Resolve(), "pev7gift");
            var t = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_ApprenticeMaster");
            if (t != null && hc.a != null) sheet.Thought(new List<Pawn> { hc.a }, hc.a.LabelShort, t, p => null);
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents7Math.ComeBackGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => ComeBackBetter.Given(ctx as HappeningContext);
    }
}
