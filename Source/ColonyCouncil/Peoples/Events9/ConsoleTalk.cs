using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // ===================== 93. The console that talks to nobody (the Sunken Vault) =====================

    /// <summary>
    /// The console that talks to nobody (item 93): the vault's old console has picked up the colony's radio, and for three evenings the
    /// overseer wants to talk only with the colony's best talker. Each evening at dusk the talker goes to the colony's radio (a powered
    /// comms console, or the Signal Line's mast) and talks an hour (Social). A colony without a radio gets the same as letters: each
    /// evening a surface-runner brings the overseer's letter to the gate and waits while the talker writes the answer at the colony's table.
    /// At the end the overseer asks the colony for something (components or medicine; a runner comes to the gate for it, the code-fired
    /// Rimfolk_Evt_ConsoleAsk), or tells where an old supply cache of the vault's lies (a site of the game's on the world map, a few tiles
    /// off, heard of). Or the talker talks one evening only. One of the events' visits (kind "pev:console"): a the talker, x the evening's
    /// runner (none by radio), due the evenings talked, stake the day of the last, its text "people|radio or letters|evenings".
    /// </summary>
    public static class ConsoleTalk
    {
        public const string Kind = "pev:console";
        public const string AskEventDef = "Rimfolk_Evt_ConsoleAsk";

        private static int Now => Find.TickManager.TicksGame;

        public static Visit Open() => CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(v => v != null && v.kind == Kind && !v.done);

        private static string[] Parts(Visit v) => (v?.text ?? "").Split('|');

        private static bool ByRadio(Visit v) => Parts(v).ElementAtOrDefault(1) == "radio";

        private static int Evenings(Visit v) => int.TryParse(Parts(v).ElementAtOrDefault(2), out int n) ? n : PeopleEvents9Math.ConsoleEvenings;

        /// <summary>The colony's best talker, up and about.</summary>
        public static Pawn Talker(Map map) => PeoplesD.Best(map, SkillDefOf.Social, p => !p.InMentalState);

        /// <summary>Where the letters are written: the colony's table nearest the gate, else the gate itself.</summary>
        public static LocalTargetInfo Table(Map map, Pawn by)
        {
            var gate = PeoplesD.GateSpot(map) ?? by.Position;
            var table = map.listerBuildings.allBuildingsColonist.Where(b => b != null && b.def.IsTable && by.CanReach(b, PathEndMode.Touch, Danger.Some))
                .OrderBy(b => b.Position.DistanceToSquared(gate)).FirstOrDefault();
            return table != null ? new LocalTargetInfo(table) : new LocalTargetInfo(gate);
        }

        /// <summary>The answer taken: the evenings kept as a visit; the first evening starts at the next dusk (or now, at dusk).</summary>
        public static void Begin(HappeningContext ctx, int evenings)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            if (map == null || t == null || ctx.a == null || ctx.faction == null) return;
            var v = t.AddVisit(Kind, ctx.incident, map, null, ctx.a, null, Now + (evenings + 2) * GenDate.TicksPerDay);
            v.text = ctx.faction.GetUniqueLoadID() + "|" + (VoiceForColony.Radio(map) != null ? "radio" : "letters") + "|" + evenings;
            v.due = 0;
            v.stake = -1;
            if (!PlayOuts.On)
            {
                for (int i = 0; i < evenings; i++) Talked(ctx.a);
                return;
            }
            Evening(v);
        }

        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var talker = v.a;
            if (talker == null || talker.Dead || now > v.until)
            {
                v.done = true;
                return false;
            }
            Evening(v);
            return !v.done;
        }

        /// <summary>At dusk, once a day: the talker to the radio, or the runner at the gate with a letter and the talker to the table.</summary>
        private static void Evening(Visit v)
        {
            var map = v.map;
            var talker = v.a;
            int day = GenDate.DaysPassed;
            if (map == null || v.stake == day || v.due >= Evenings(v) || !PeopleEvents9Math.ConsoleHour(GenLocalDate.HourOfDay(map))) return;
            if (!PeoplesG.Up(talker, map) || talker.Drafted || MapFacts.ThreatOn(map)) return;
            var radio = VoiceForColony.Radio(map);
            LocalTargetInfo at;
            if (ByRadio(v) && radio != null) at = radio;
            else
            {
                // The letters: a surface-runner with the overseer's letter at the gate, and the answer written at the colony's table.
                var f = PeopleEvents9.PeopleOf(v);
                var runner = f == null ? null : Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_SunkenVault_Scout"), f, new FloatRange(16f, 40f));
                if (runner == null || Arrivals.Visit(new List<Pawn> { runner }, map, f, PeoplesD.GateSpot(map), 4f) == null) return;
                if (v.x != null && v.x != runner && v.x.Spawned) PeoplesG.Home(new List<Pawn> { v.x });
                v.x = runner;
                // One runner an evening: if the answer can't be written now, they wait their hours at the gate and go.
                v.stake = day;
                at = Table(map, talker);
            }
            if (PeopleEvents9.Send(talker, PeopleEvents9.ConsoleJob, at, PeopleEvents9Math.ConsoleTalkTicks)) v.stake = day;
        }

        /// <summary>An evening's talk (or letter) done: the talker the better at it, a word of what was said, and after the last, the end.</summary>
        public static void Talked(Pawn talker)
        {
            var v = CouncilGameComponent.Instance?.happenings?.visits?.FirstOrDefault(x => x != null && x.kind == Kind && !x.done && x.a == talker);
            if (v == null) return;
            v.due++;
            var map = v.map;
            var f = PeopleEvents9.PeopleOf(v);
            talker.skills?.Learn(SkillDefOf.Social, PeopleEvents9Math.ConsoleSocialXp, true);
            if (v.x != null && v.x.Spawned) PeoplesG.Home(new List<Pawn> { v.x });
            string key = (ByRadio(v) ? "CC_PEv9_ConsoleRadio_" : "CC_PEv9_ConsoleLetter_") + Mathf.Clamp(v.due, 1, 3);
            Tell.Message(NewsKind.People, Importance.Notable, map, key.Translate(talker.Named("PAWN"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                talker.Spawned ? new LookTargets(talker) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            if (v.due < Evenings(v)) return;
            v.done = true;
            End(v, f, talker);
        }

        /// <summary>The talk over: one evening, a little thanks; three, the overseer's ask or the cache.</summary>
        private static void End(Visit v, Faction f, Pawn talker)
        {
            var map = v.map;
            if (Evenings(v) < PeopleEvents9Math.ConsoleEvenings)
            {
                PeopleEvents9.Goodwill(f, PeopleEvents9Math.OneEveningGoodwill, GoodwillWhy.Friendship);
                return;
            }
            PeopleEvents9.Goodwill(f, PeopleEvents9Math.ConsoleGoodwill, GoodwillWhy.Friendship);
            if (f == null || map == null) return;
            if (!PeopleEvents9Math.TellsOfCache(Rand.Value))
            {
                asking = talker;
                try
                {
                    if (PeopleEvents2.Fire(AskEventDef, map, f)) return;
                }
                finally
                {
                    asking = null;
                }
            }
            Cache(map, f, talker, ByRadio(v));
        }

        /// <summary>The talker the ask is fired for, while it is.</summary>
        private static Pawn asking;

        public static Pawn Asking => asking;

        /// <summary>The overseer tells where an old supply cache lies: the game's item stash a few tiles off, with the vault's old stores, heard of.</summary>
        public static bool Cache(Map map, Faction f, Pawn talker, bool radio)
        {
            var part = DefDatabase<SitePartDef>.GetNamedSilentFail("ItemStash");
            if (part == null || !TileFinder.TryFindNewSiteTile(out var tile, map.Tile, PeopleEvents9Math.CacheTilesMin, PeopleEvents9Math.CacheTilesMax, false, null, 0f)) return false;
            try
            {
                var site = SiteMaker.MakeSite(part, tile, null, true, 0f);
                if (site == null) return false;
                site.customLabel = "CC_PEv9_CacheLabel".Translate(f.Name.Named("FACTION")).Resolve().CapitalizeFirst();
                site.GetComponent<TimeoutComp>()?.StartTimeout(PeopleEvents9Math.CacheDays * GenDate.TicksPerDay);
                var sp = site.parts?.FirstOrDefault();
                if (sp != null)
                {
                    sp.things ??= new ThingOwner<Thing>(sp, false);
                    foreach (var t in Stores(f)) sp.things.TryAdd(t);
                }
                Find.WorldObjects.Add(site);
                string way = NeighbourLetters.Direction(map.Tile, tile);
                int tiles = Mathf.Max(1, Mathf.RoundToInt(Find.WorldGrid.ApproxDistanceInTiles(map.Tile, tile)));
                string what = "CC_PEv9_CacheHeard".Translate(f.Name.Named("FACTION")).Resolve();
                HeardPlaces.Heard(new[] { "site:" + site.ID }, what, f.Name, talker?.LabelShort);
                Tell.Letter(NewsKind.World, Importance.Important, map, "CC_PEv9_CacheLetterLabel".Translate().Resolve(),
                    (radio ? "CC_PEv9_CacheRadio" : "CC_PEv9_CacheLetter").Translate((talker?.LabelShort ?? "?").Named("PAWN"), f.Name.Named("FACTION"), tiles.Named("COUNT"), way.Named("WAY"))
                    .Resolve(), LetterDefOf.PositiveEvent, new LookTargets(site), f);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 9: the vault's cache", e);
                return false;
            }
        }

        /// <summary>The old stores in the cache: components, survival packs, medicine, and one of the vault's last goods.</summary>
        public static List<Thing> Stores(Faction f)
        {
            var list = new List<Thing>();

            void Add(ThingDef d, int n)
            {
                if (d == null || n <= 0) return;
                var t = ThingMaker.MakeThing(d, d.MadeFromStuff ? GenStuff.DefaultStuffFor(d) : null);
                t.stackCount = Mathf.Min(d.stackLimit, n);
                list.Add(t);
            }

            Add(ThingDefOf.ComponentIndustrial, Rand.RangeInclusive(4, 8));
            Add(ThingDefOf.MealSurvivalPack, Rand.RangeInclusive(6, 12));
            Add(ThingDefOf.MedicineIndustrial, Rand.RangeInclusive(3, 6));
            var last = HappeningWorker_VaultBarter.Crate(f).RandomElementWithFallback();
            if (last.def != null) Add(last.def, last.count);
            return list;
        }
    }

    /// <summary>
    /// The vault's console has picked up the colony's radio ({TALE}), and the overseer of {FACTION} asks to talk with {A} in the evenings (or, a
    /// colony without a radio, a surface-runner {X} at the gate with the overseer's first letter).
    /// </summary>
    public class HappeningWorker_ConsoleTalk : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Vaultfolk) != null && ConsoleTalk.Open() == null && ConsoleTalk.Talker(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Vaultfolk);
            ctx.a = ConsoleTalk.Talker(ctx.map);
            if (ctx.faction == null || ctx.a == null) return false;
            var radio = VoiceForColony.Radio(ctx.map);
            ctx.variant = radio != null ? 1 : 0;
            ctx.place = radio;
            ctx.tale = radio?.LabelShort;
            if (radio == null)
            {
                ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SunkenVault_Scout"), ctx.faction, ext.ages);
                if (ctx.stranger == null) return false;
            }
            ctx.count = PeopleEvents9Math.ConsoleEvenings;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            ctx.stranger == null || Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.variant > 0 ? "radio" : "letters");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.a == null || ctx.a.Dead || ctx.a.MapHeld != ctx.map) ? "CC_PEv9_TalkerGone".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault && ctx.stranger != null) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "letter" : "later");
            // The first letter's runner goes back with the answer; each evening's letter comes with a runner of its own.
            if (ctx.stranger != null && option <= 1) Arrivals.SendAway(PeoplesG.Party(ctx));
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The evenings with the overseer: <see cref="evenings"/> of them (three, or one only).</summary>
    public class DilemmaEffect_ConsoleTalk : DilemmaEffect
    {
        public int evenings = PeopleEvents9Math.ConsoleEvenings;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null) return;
            sheet.Play((hc.variant > 0 ? "CC_PEv9_Impact_TalkRadio" : "CC_PEv9_Impact_TalkLetters").Translate(hc.a.LabelShort, evenings).Resolve(), "CC_PEv9_Impact_TalkBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Skill, 1, "CC_PEv9_Impact_TalkSocial".Translate(hc.a.LabelShort, SkillDefOf.Social.label).Resolve(), "CC_PEv9_Impact_TalkSocialBrief".Translate().Resolve(), "pev9talk");
            if (evenings >= PeopleEvents9Math.ConsoleEvenings)
                sheet.Add(ImpactKind.Other, 1, "CC_PEv9_Impact_TalkEnd".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv9_Impact_TalkEndBrief".Translate().Resolve(), "pev9talkend");
            if (hc.faction != null) sheet.Goodwill(hc.faction, evenings >= PeopleEvents9Math.ConsoleEvenings ? PeopleEvents9Math.ConsoleGoodwill : PeopleEvents9Math.OneEveningGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => ConsoleTalk.Begin(ctx as HappeningContext, evenings);
    }

    /// <summary>
    /// The overseer's ask at the end of the evenings: a surface-runner of {FACTION} ({X}) at the gate for {COUNT} {THING}, as the overseer asked
    /// {A}. Fired from code (ConsoleTalk); it doesn't come on the storyteller's draw.
    /// </summary>
    public class HappeningWorker_ConsoleAsk : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Vaultfolk) != null && ConsoleTalk.Asking != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Vaultfolk);
            ctx.a = ConsoleTalk.Asking;
            if (ctx.faction == null || ctx.a == null) return false;
            float wf = LivingEvents.WealthFactor(ctx.map);
            bool medicine = Rand.Bool;
            ctx.thing = medicine ? ThingDefOf.MedicineIndustrial : ThingDefOf.ComponentIndustrial;
            ctx.count = medicine ? PeopleEvents9Math.AskMedicine(wf) : PeopleEvents9Math.AskComponents(wf);
            ctx.variant = Mathf.Max(1, ctx.count / 2);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_SunkenVault_Scout"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.thing == ThingDefOf.MedicineIndustrial ? "medicine" : "components");
            if (VoiceForColony.Radio(ctx.map) != null) tags.Add("radio");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "thanks" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>What the colony gave the overseer's runner, remembered as supply (all of it, or <see cref="half"/>).</summary>
    public class DilemmaEffect_ConsoleGive : DilemmaEffect
    {
        public bool half;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            sheet.Add(ImpactKind.Other, 1, "CC_PeoplesG_Impact_Supply".Translate(ctx?.faction?.Name ?? "?").Resolve(), "CC_PeoplesG_Impact_SupplyBrief".Translate().Resolve(), "vaultsupply");
            if (ctx?.faction != null) sheet.Goodwill(ctx.faction, half ? PeopleEvents9Math.AskGoodwill / 2 : PeopleEvents9Math.AskGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.faction == null || hc.thing == null) return;
            int n = half ? hc.variant : hc.count;
            VaultSupply.Noted(hc.faction, n * hc.thing.BaseMarketValue, (hc.thing == ThingDefOf.MedicineIndustrial ? "CC_PEv9_SupplyMedicine" : "CC_PeopleAge_Gift_components").Translate().Resolve());
            PeopleEvents9.Goodwill(hc.faction, half ? PeopleEvents9Math.AskGoodwill / 2 : PeopleEvents9Math.AskGoodwill, GoodwillWhy.Gift);
        }
    }
}
