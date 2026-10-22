using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // A fifth batch of world events, written for the storyteller's own pool (Misc, and ThreatSmall for none of them:
    // the two that can end in a fight only do so as the risk of an answer). People passing through, deals that pay
    // later or not at all, a colonist's past knocking, things each era meets on its own (a lord's tithe, a hart at the
    // gate, a drone in the field, a signal on the comms), and one for A RimWorld of Magic. The land and the colony's own
    // small finds are in Workers_Storyteller_Land.cs. Texts: Defs/IncidentDefs/Council_Events_Storyteller*.xml.

    /// <summary>
    /// What the storyteller batch shares: its memos (things on their way, and memories later events can pick up),
    /// their hourly round and World page lines, and helpers for places and things on the map.
    /// </summary>
    public static class StorytellerEvents
    {
        // ---- Things on their way: each has a due tick and is told when it comes due. ----

        /// <summary>Silver put into a merchant's venture. faction: the merchant's people; other: who dealt with them; amount: the silver; def: how it ends (good, lost, con); text: the merchant's name.</summary>
        public const string Venture = "venture";

        /// <summary>Supplies sent to an old commander's campaign. faction: their people; other: the old soldier; def and amount: what was sent; text: the commander's name; times: the enemy's load id.</summary>
        public const string Campaign = "campaign";

        /// <summary>The silent guest leaves something behind. faction: their people; other: who they sat with (or null); text: their name.</summary>
        public const string MonkGift = "monkgift";

        /// <summary>Never shown: food and bandages left for a wounded stranger. def: "gift" or "none" (settled when they were left); other: who found them; text: their name.</summary>
        public const string Mended = "mended";

        // ---- Memories a later event can pick up (they fade after KeepDays). ----

        /// <summary>Peace bought from a hostile people. faction: them; other: who met their envoy.</summary>
        public const string Peace = "peace";

        /// <summary>A prisoner handed over to a neighbor. faction: the neighbor; text: the prisoner's name; amount: the load id of the prisoner's own people.</summary>
        public const string HandedOver = "handedover";

        /// <summary>Someone who knows a colonist's outlaw past. pawn: them; faction: their people; other: the colonist; won: paid off, or the bluff held.</summary>
        public const string Recognized = "recognized";

        /// <summary>A blade forged from a fallen star. other: the smith; def: the weapon; text: what it is.</summary>
        public const string StarBlade = "starblade";

        /// <summary>A survivor who answered the colony's call and came down in a pod. pawn: them (a colonist now).</summary>
        public const string Survivor = "survivor";

        /// <summary>The old adventurer who stayed. pawn: them (a colonist now).</summary>
        public const string Adventurer = "adventurer";

        /// <summary>The event whose texts tell each kind of memo.</summary>
        private static readonly Dictionary<string, string> Events = new Dictionary<string, string>
        {
            { Venture, "Council_Evt_Venture" }, { Campaign, "Council_Evt_OldCommander" }, { MonkGift, "Council_Evt_SilentMonk" },
            { Mended, "Council_Evt_WoundedStranger" }, { Peace, "Council_Evt_Envoy" }, { HandedOver, "Council_Evt_WantedPrisoner" },
            { Recognized, "Council_Evt_Recognized" }, { StarBlade, "Council_Evt_StarIron" }, { Survivor, "Council_Evt_Signal" },
            { Adventurer, "Council_Evt_OldAdventurer" }
        };

        private static readonly HashSet<string> PendingKinds = new HashSet<string> { Venture, Campaign, MonkGift, Mended };

        /// <summary>On the World page's "what's coming" (a gift that may never come isn't).</summary>
        private static readonly HashSet<string> Shown = new HashSet<string> { Venture, Campaign };

        private static readonly Dictionary<string, float> KeepDays = new Dictionary<string, float>
        {
            { Peace, 180f }, { HandedOver, 150f }, { Recognized, 150f }, { StarBlade, 365f }, { Survivor, 365f }, { Adventurer, 365f }
        };

        private static int Now => Find.TickManager.TicksGame;

        private static HappeningTracker Tracker => CouncilGameComponent.Instance?.happenings;

        public static bool IsPending(Memo m) => m != null && !m.used && m.kind != null && PendingKinds.Contains(m.kind);

        public static IncidentDef EventOf(string kind) =>
            kind != null && Events.TryGetValue(kind, out var name) ? DefDatabase<IncidentDef>.GetNamedSilentFail(name) : null;

        public static HappeningLivingText More(IncidentDef def) => def?.GetModExtension<HappeningLivingText>();

        /// <summary>Something on its way: comes due in <paramref name="days"/>.</summary>
        public static Memo Schedule(string kind, Pawn pawn, Faction f, string def, string text, Pawn other, float days)
        {
            var t = Tracker;
            if (t == null) return null;
            var m = t.Remember(kind, pawn, f, def, text, false, other);
            m.due = Now + Mathf.RoundToInt(days * GenDate.TicksPerDay);
            return m;
        }

        // ---------------- Every hour ----------------

        public static void Hourly(HappeningTracker t, CouncilGameComponent comp, int now)
        {
            bool home = HappeningTracker.HomeMap() != null;
            foreach (var m in t.memos.Where(m => IsPending(m) && now >= m.due).ToList())
            {
                // No home right now (a gravship in flight, a caravan life): it waits.
                if (!home)
                {
                    m.due = now + GenDate.TicksPerHour;
                    continue;
                }
                // Used first, so one that throws can't come round every hour.
                t.Use(m);
                try
                {
                    switch (m.kind)
                    {
                        case Venture: HappeningWorker_Venture.Return(m); break;
                        case Campaign: HappeningWorker_OldCommander.Report(m); break;
                        case MonkGift: HappeningWorker_SilentMonk.Gift(m); break;
                        case Mended: HappeningWorker_WoundedStranger.Repaid(m); break;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("storyteller event " + m.kind, e);
                }
            }
            if (now % GenDate.TicksPerDay < GenDate.TicksPerHour) Fade(t, now);
        }

        private static void Fade(HappeningTracker t, int now)
        {
            foreach (var m in t.memos.Where(m => !m.used && m.kind != null && KeepDays.ContainsKey(m.kind)).ToList())
                if (now - m.tick > KeepDays[m.kind] * GenDate.TicksPerDay) t.Use(m);
        }

        // ---------------- Telling ----------------

        /// <summary>The people, things and places a memo is about, for its texts.</summary>
        public static HappeningContext Context(Memo m, IncidentDef def, Map map)
        {
            var ctx = new HappeningContext { incident = def, map = map, faction = m.faction, a = m.other, count = m.amount, tale = m.text };
            switch (m.kind)
            {
                case Campaign:
                    ctx.enemy = LivingEvents.FactionById(m.times);
                    ctx.thing = LivingEvents.ThingNamed(m.def);
                    ctx.placeName = VarietyUtil.Bundle(ctx.thing, m.amount);
                    break;
                case HandedOver:
                    ctx.enemy = LivingEvents.FactionById(m.amount);
                    break;
                case Recognized:
                    ctx.stranger = m.pawn;
                    break;
            }
            return ctx;
        }

        /// <summary>A line that stays the same while <paramref name="seed"/> does (the World page is drawn every frame).</summary>
        private static string Stable(List<string> lines, IEnumerable<string> tags, int seed)
        {
            if (lines.NullOrEmpty()) return null;
            var context = new HashSet<string>();
            Flavor.AddEraTags(context);
            if (tags != null)
                foreach (var tag in tags)
                    context.Add(tag);
            var fit = lines.Where(l => !l.NullOrEmpty() && Flavor.TechFits(l) && Flavor.Match(l, context) >= 0).ToList();
            if (fit.Count == 0) return null;
            return Flavor.Strip(fit[(seed % fit.Count + fit.Count) % fit.Count]);
        }

        /// <summary>Something on its way, for the World page's "what's coming".</summary>
        public static string WorldLine(Memo m, int now)
        {
            if (!IsPending(m) || !Shown.Contains(m.kind)) return null;
            var def = EventOf(m.kind);
            string line = Stable(More(def)?.coming, null, m.tick);
            if (line.NullOrEmpty()) return null;
            return Context(m, def, null).Format(line.Replace("{DAYS}", TextUtil.DaysString(Mathf.Max(0, m.due - now))));
        }

        /// <summary>A memory waiting for a later event, for the World page's "who's out there".</summary>
        public static string MemoLine(Memo m)
        {
            if (m == null || m.used || m.kind == null) return null;
            switch (m.kind)
            {
                case Peace:
                    if (!LivingEvents.Friendly(m.faction)) return null;
                    break;
                case HandedOver:
                    if (m.faction == null || m.faction.defeated || LivingEvents.FactionById(m.amount) == null) return null;
                    break;
                case Recognized:
                    if (m.pawn == null || m.pawn.Dead || m.other == null || m.other.Dead || !m.other.IsColonist) return null;
                    break;
                default:
                    return null;
            }
            var def = EventOf(m.kind);
            string line = Stable(More(def)?.remembered, new[] { m.won ? "quiet" : "talked" }, m.tick);
            return line.NullOrEmpty() ? null : Context(m, def, null).Format(line);
        }

        // ---------------- Places and things ----------------

        /// <summary>Where the colony is: the middle of its people at home, or of the map.</summary>
        public static IntVec3 Center(Map map)
        {
            var people = map.mapPawns.FreeColonistsSpawned;
            if (people.Count == 0) return map.Center;
            int x = 0, z = 0;
            foreach (var p in people)
            {
                x += p.Position.x;
                z += p.Position.z;
            }
            return new IntVec3(x / people.Count, 0, z / people.Count);
        }

        private static readonly string[] Compass = { "East", "NorthEast", "North", "NorthWest", "West", "SouthWest", "South", "SouthEast" };

        /// <summary>"to the north", seen from the colony.</summary>
        public static string Direction(Map map, IntVec3 cell)
        {
            var c = Center(map);
            float dx = cell.x - c.x, dz = cell.z - c.z;
            if (dx * dx + dz * dz < 25f) return "CC_St_Dir_Near".Translate();
            float angle = (Mathf.Atan2(dz, dx) * Mathf.Rad2Deg + 360f) % 360f;
            return ("CC_St_Dir_" + Compass[Mathf.RoundToInt(angle / 45f) % 8]).Translate();
        }

        /// <summary>Open ground out on the land: nothing built or lying there, nobody standing there, not in the home area.</summary>
        public static bool OpenGround(IntVec3 c, Map map) =>
            c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && !c.Roofed(map) && !map.areaManager.Home[c] && c.GetEdifice(map) == null
            && c.GetFirstItem(map) == null && c.GetFirstPawn(map) == null && c.GetZone(map) == null && !c.GetTerrain(map).IsWater;

        /// <summary>Open ground at a distance from the colony.</summary>
        public static bool TryOpenSpot(Map map, float minDist, float maxDist, out IntVec3 cell, Predicate<IntVec3> extra = null)
        {
            var center = Center(map);
            return CellFinderLoose.TryGetRandomCellWith(c => OpenGround(c, map) && c.DistanceTo(center) >= minDist && c.DistanceTo(center) <= maxDist
                                                             && (extra == null || extra(c)), map, 600, out cell);
        }

        public static bool NaturalRock(IntVec3 c, Map map) => c.InBounds(map) && c.GetEdifice(map)?.def.building?.isNaturalRock == true;

        /// <summary>A seam's ore: common metal mostly, finer things now and then, and nothing the colony's era wouldn't know.</summary>
        public static ThingDef Ore(bool rich)
        {
            var options = new List<(string name, float weight)> { ("MineableSteel", rich ? 3f : 5f), ("MineableSilver", 2.5f), ("MineableJade", 1f), ("MineableGold", rich ? 1f : 0.5f) };
            if (!Avail.IsPreIndustrial)
            {
                options.Add(("MineableComponentsIndustrial", 2f));
                if (Avail.EffectiveTech >= TechLevel.Spacer) options.Add(("MineablePlasteel", 1f));
            }
            return options.Select(o => (def: DefDatabase<ThingDef>.GetNamedSilentFail(o.name), o.weight))
                .Where(o => o.def?.building?.mineableThing != null).RandomElementByWeightWithFallback(o => o.weight).def;
        }

        /// <summary>Turns up to <paramref name="n"/> cells of natural rock around a spot into ore (nearest first). Returns what it made.</summary>
        public static List<Thing> Seam(Map map, IntVec3 at, ThingDef ore, int n)
        {
            var made = new List<Thing>();
            if (ore == null) return made;
            foreach (var c in GenRadial.RadialCellsAround(at, 3.2f, true))
            {
                if (made.Count >= n) break;
                if (!NaturalRock(c, map) || c.GetEdifice(map).def == ore) continue;
                made.Add(GenSpawn.Spawn(ore, c, map, WipeMode.Vanish));
            }
            return made;
        }

        /// <summary>A face of natural rock the colony can see, some way off.</summary>
        public static bool TryRockFace(Map map, float minDist, out IntVec3 cell)
        {
            var center = Center(map);
            return CellFinderLoose.TryGetRandomCellWith(c => NaturalRock(c, map) && !c.Fogged(map) && c.DistanceTo(center) >= minDist
                                                             && GenAdj.AdjacentCells.Any(d => (c + d).InBounds(map) && (c + d).Standable(map)), map, 1500, out cell);
        }

        /// <summary>Things handed over near a spot (or at the trade spot).</summary>
        public static Thing Hand(ThingDef def, int count, Map map, IntVec3 near)
        {
            if (def == null || count <= 0 || map == null) return null;
            var cell = near.IsValid && near.InBounds(map) ? near : DropCellFinder.TradeDropSpot(map);
            Thing first = null;
            while (count > 0)
            {
                var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                thing.stackCount = Mathf.Min(count, Mathf.Max(1, def.stackLimit));
                count -= thing.stackCount;
                GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
                if (first == null && thing.Spawned) first = thing;
            }
            return first;
        }

        /// <summary>A round sum of silver, more for a richer colony.</summary>
        public static int Silver(Map map, FloatRange range) => Mathf.Max(10, Mathf.RoundToInt(range.RandomInRange * LivingEvents.WealthFactor(map) / 10f) * 10);

        /// <summary>An adult at home who isn't down, busy losing their mind or a guest.</summary>
        public static bool Able(Pawn p) => p != null && !p.DevelopmentalStage.Juvenile() && p.Spawned && !p.Downed && !p.InMentalState;

        public static IEnumerable<Pawn> Adults(Map map) => IncidentWorker_Happening.Colonists(map).Where(Able);

        /// <summary>The colony's best adult at a skill, or any adult.</summary>
        public static Pawn BestOrAny(Map map, SkillDef skill) => VarietyUtil.Best(map, skill) ?? Adults(map).RandomElementWithFallback();

        /// <summary>A visitor is good at what they do.</summary>
        public static void Skilled(Pawn p, SkillDef skill, IntRange level)
        {
            var r = p?.skills?.GetSkill(skill);
            if (r != null && !r.TotallyDisabled && r.Level < level.min) r.Level = level.RandomInRange;
        }

        /// <summary>Who someone used to be, from their backstory, with its article: "a smuggler", "an assassin".</summary>
        public static string WhoTheyWere(Pawn p)
        {
            string title = Pasts.Title(p);
            return title.NullOrEmpty() ? "" : Find.ActiveLanguageWorker.WithIndefiniteArticle(title);
        }

        /// <summary>Why an answer can't be given any more: "The wreck is gone."</summary>
        public static string Gone(string theThing) => "CC_St_Gone".Translate(theThing).CapitalizeFirst().ToString();

        public static ThoughtDef Thought(string defName) => DefDatabase<ThoughtDef>.GetNamedSilentFail(defName);

        /// <summary>A mood for someone of the colony, caused by the event.</summary>
        public static void Give(Pawn p, string thought, IncidentDef def, Pawn about = null)
        {
            // apart: the helper events give their memories through.
            if (p == null || p.Dead || !p.IsColonist) return;
            using (Causes.Push(LivingEvents.Cause(def)))
                CouncilGameComponent.GiveThought(p, Thought(thought), about);
        }

        /// <summary>Food in the stores, for paying in kind.</summary>
        public static DilemmaEffect_Consume Food(int count) => new DilemmaEffect_Consume { what = "rawfood", count = count };

        /// <summary>"60 raw food".</summary>
        public static string FoodLabel(int n) => n + " " + "CC_Dilemma_RawFood".Translate();

        private static ThingDef honey;
        private static bool honeyLooked;

        /// <summary>Honey, when a mod has it (Alpha Bees, Medieval Overhaul, Vanilla Cooking...), or null.</summary>
        public static ThingDef Honey()
        {
            if (honeyLooked) return honey;
            honeyLooked = true;
            foreach (var name in new[] { "RB_BaseHoney", "VCE_Honey", "Honey" })
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (d != null && d.category == ThingCategory.Item && d.IsIngestible)
                {
                    honey = d;
                    return honey;
                }
            }
            honey = DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(d => d.category == ThingCategory.Item && d.IsIngestible && !d.IsDrug && d.label == "honey");
            return honey;
        }
    }

    // ===================== The old adventurer =====================

    /// <summary>
    /// An old adventurer, done with the road, sells their kit: a fine weapon and armor. Or the colony asks them to stay
    /// (they may say no, and they bring the kit if they don't), or just listens to their stories for an evening.
    /// </summary>
    public class HappeningWorker_OldAdventurer : HappeningWorker_Visitors
    {
        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Shooting, new IntRange(8, 13));
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Melee, new IntRange(8, 13));
            // What the kit is worth (made when it changes hands), and its price.
            ctx.variant = Mathf.RoundToInt(ext.value.RandomInRange * LivingEvents.WealthFactor(ctx.map));
            ctx.count = Mathf.Max(50, Mathf.RoundToInt(ctx.variant * 0.55f / 10f) * 10);
            ctx.a = VarietyUtil.Best(ctx.map, SkillDefOf.Social) ?? StorytellerEvents.Adults(ctx.map).RandomElementWithFallback();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, GatheringSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var x = ctx.stranger;
            var near = x != null && x.Spawned && x.Map == ctx.map ? x.Position : VarietyUtil.Near(ctx.a, ctx.map);
            // Bought, or brought along by an adventurer who stays.
            // Bought: the adventurer hands the kit over (Couriers/); brought along by one who stays: set down as theirs.
            if (option == 0 && !byDefault) Couriers.Loot(Loot.Make(ctx.variant, true), ctx.map, near, Couriers.PeopleOf(ctx), x);
            else if (option == 1 && !risk && x != null && x.Faction == Faction.OfPlayer) Loot.Deliver(Loot.Make(ctx.variant, true), ctx.map, near, false);
            if (option == 1 && !risk && x != null && x.Faction == Faction.OfPlayer)
                CouncilGameComponent.Instance?.happenings.Remember(StorytellerEvents.Adventurer, x, null, null, null, false, ctx.a);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The mapmaker =====================

    /// <summary>
    /// A mapmaker surveying the region offers to survey the colony's own land: paid, they find a seam of ore in the
    /// rock; guided by the colony's cleverest, for free, they may find a smaller one, or nothing.
    /// </summary>
    public class HappeningWorker_Cartographer : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && StorytellerEvents.TryRockFace(map, 12f, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Intellectual, new IntRange(9, 15));
            ctx.count = StorytellerEvents.Silver(ctx.map, ext.value);
            ctx.a = StorytellerEvents.BestOrAny(ctx.map, SkillDefOf.Intellectual);
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault && (option == 0 || (option == 1 && !risk)) && ctx.map != null)
            {
                var ore = StorytellerEvents.Ore(option == 0);
                if (ore != null && StorytellerEvents.TryRockFace(ctx.map, 12f, out var cell))
                {
                    var seam = StorytellerEvents.Seam(ctx.map, cell, ore, option == 0 ? Rand.RangeInclusive(6, 10) : Rand.RangeInclusive(3, 6));
                    if (seam.Count > 0)
                    {
                        // By the name the map shows it under: "compacted steel", "silver", "jade".
                        ctx.thing = ore;
                        ctx.placeName = StorytellerEvents.Direction(ctx.map, cell);
                        var more = StorytellerEvents.More(def);
                        LivingEvents.Tell(ctx, more?.doneTitles, more?.done, null, LetterDefOf.PositiveEvent, new LookTargets(seam[0]));
                    }
                }
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The silent guest =====================

    /// <summary>
    /// A monk (a spirit-walker, an ascetic) under a vow of silence asks to sit near the colony for a day or two. Let
    /// them, and the colony is calmer for it, and they leave something behind; or ask them to sit with whoever is
    /// struggling most.
    /// </summary>
    public class HappeningWorker_SilentMonk : HappeningWorker_Visitors
    {
        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            // Whoever is struggling most.
            ctx.a = StorytellerEvents.Adults(ctx.map).OrderBy(p => p.needs?.mood?.CurLevelPercentage ?? 0.5f).FirstOrDefault();
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if ((ctx.a?.needs?.mood?.CurLevelPercentage ?? 1f) < 0.4f) tags.Add("troubled");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && ctx.stranger != null)
                StorytellerEvents.Schedule(StorytellerEvents.MonkGift, null, ctx.faction, null, ctx.stranger.LabelShort, option == 1 ? ctx.a : null, Rand.Range(1.2f, 1.6f));
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>Gone before dawn, with something left on the bench: a small carving, or a few things of use.</summary>
        public static void Gift(Memo m)
        {
            var def = StorytellerEvents.EventOf(m.kind);
            var map = HappeningTracker.HomeMap();
            if (def == null || map == null) return;
            var ctx = StorytellerEvents.Context(m, def, map);
            var more = StorytellerEvents.More(def);
            var spot = GatheringSpot(map) ?? DropCellFinder.TradeDropSpot(map);
            Thing gift = null;
            string what;
            var carving = DefDatabase<ThingDef>.GetNamedSilentFail("SculptureSmall");
            var wood = ThingDefOf.WoodLog;
            if (carving != null && carving.Minifiable && wood?.stuffProps != null && wood.stuffProps.CanMake(carving))
            {
                var art = ThingMaker.MakeThing(carving, wood);
                art.TryGetComp<CompQuality>()?.SetQuality(Rand.Chance(0.4f) ? QualityCategory.Excellent : QualityCategory.Good, ArtGenerationContext.Outsider);
                what = art.LabelNoCount;
                gift = MinifyUtility.MakeMinified(art);
                GenPlace.TryPlaceThing(gift, spot, map, ThingPlaceMode.Near);
            }
            else
            {
                var things = Loot.Make(Rand.Range(120f, 220f) * LivingEvents.WealthFactor(map));
                Loot.Deliver(things, map, spot, false);
                gift = things.FirstOrDefault(t => t.Spawned);
                what = Loot.Describe(things);
            }
            LivingEvents.Tell(ctx, more?.doneTitles, more?.done, new[] { m.other != null ? "sat" : "stayed" }, LetterDefOf.PositiveEvent,
                gift != null && gift.Spawned ? new LookTargets(gift) : LookTargets.Invalid, what);
        }
    }

    // ===================== The limb-maker =====================

    /// <summary>
    /// A maker of artificial limbs (a peg leg and a wooden foot before electricity, simple prosthetics after) offers to
    /// fit a colonist who lost one. Paid in silver or in the materials; the colony's doctor watches and learns.
    /// </summary>
    public class HappeningWorker_LimbMaker : HappeningWorker_Visitors
    {
        /// <summary>The first missing limb on <paramref name="p"/> the limb-maker can replace: where it goes, and with what.</summary>
        public static bool Fit(Pawn p, out BodyPartRecord part, out HediffDef limb)
        {
            part = null;
            limb = null;
            var missing = p?.health?.hediffSet?.GetMissingPartsCommonAncestors();
            if (missing == null) return false;
            bool low = Avail.IsPreIndustrial;
            foreach (var m in missing)
            {
                var at = m.Part;
                string name = at?.def?.defName;
                if (name == null) continue;
                HediffDef def = null;
                BodyPartRecord on = at;
                switch (name)
                {
                    case "Leg":
                        def = DefDatabase<HediffDef>.GetNamedSilentFail(low ? "PegLeg" : "SimpleProstheticLeg");
                        break;
                    case "Foot":
                        def = DefDatabase<HediffDef>.GetNamedSilentFail(low ? "WoodenFoot" : "SimpleProstheticLeg");
                        if (!low) on = at.parent;
                        break;
                    case "Arm":
                        if (!low) def = DefDatabase<HediffDef>.GetNamedSilentFail("SimpleProstheticArm");
                        break;
                    case "Hand":
                        if (!low)
                        {
                            def = DefDatabase<HediffDef>.GetNamedSilentFail("SimpleProstheticArm");
                            on = at.parent;
                        }
                        break;
                    case "Jaw":
                        def = DefDatabase<HediffDef>.GetNamedSilentFail("Denture");
                        break;
                }
                if (def == null || on == null) continue;
                part = on;
                limb = def;
                return true;
            }
            return false;
        }

        private static IEnumerable<Pawn> Patients(Map map) => Colonists(map).Where(p => p.Spawned && !p.Dead && Fit(p, out _, out _));

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Patients(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            ctx.a = Patients(ctx.map).RandomElementWithFallback();
            if (ctx.faction == null || ctx.a == null || !Fit(ctx.a, out _, out var limb)) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Crafting, new IntRange(8, 14));
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Medicine, new IntRange(6, 12));
            ctx.note = limb.defName;
            ctx.tale = limb.label;
            float worth = limb.spawnThingOnRemoved?.BaseMarketValue ?? 0f;
            if (worth <= 0f) worth = Avail.IsPreIndustrial ? 80f : 400f;
            ctx.count = Mathf.Max(40, Mathf.RoundToInt(worth * 0.85f / 5f) * 5);
            // Or the same in materials: wood before electricity, steel after.
            ctx.thing = Avail.IsPreIndustrial ? ThingDefOf.WoodLog : ThingDefOf.Steel;
            ctx.variant = VarietyUtil.Round(Mathf.Max(10, Mathf.RoundToInt(ctx.count * 1.15f / Mathf.Max(0.5f, ctx.thing.BaseMarketValue))));
            // {PLACE} in the texts: the price in materials ("wood x95").
            ctx.placeName = VarietyUtil.Bundle(ctx.thing, ctx.variant);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        private static string Kind(string limb)
        {
            if (limb == null) return "leg";
            if (limb.Contains("Arm")) return "arm";
            if (limb.Contains("Denture")) return "jaw";
            if (limb.Contains("Foot")) return "foot";
            return "leg";
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(Kind(ctx.note));

        protected override string Extra(HappeningContext ctx) =>
            VarietyUtil.Line(StorytellerEvents.More(def)?.extra, ctx, null, VarietyUtil.Bundle(ctx.thing, VarietyUtil.InStores(ctx.map, ctx.thing)));

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && !LivingEvents.AtHome(ctx.a, ctx.map)) return "CC_Stranger_Away".Translate(ctx.a?.LabelShort ?? "?").ToString();
            if (option == 1 && ctx.thing != null && VarietyUtil.InStores(ctx.map, ctx.thing) < ctx.variant)
                return "CC_Dilemma_NotEnough".Translate(ctx.variant, ctx.thing.label).ToString();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && !byDefault && LivingEvents.AtHome(ctx.a, ctx.map) && Fit(ctx.a, out var part, out var limb)
                && (option == 0 || Couriers.TakeFromStoresFor(ctx, ctx.thing, ctx.variant)))
            {
                ctx.a.health.RestorePart(part);
                ctx.a.health.AddHediff(limb, part);
                ctx.tale = limb.label;
                StorytellerEvents.Give(ctx.a, "Council_Evt_NewLimb", def);
                // The colony's doctor watched every step.
                var doctor = StorytellerEvents.Adults(ctx.map).Where(p => p != ctx.a && p.skills?.GetSkill(SkillDefOf.Medicine) is SkillRecord r && !r.TotallyDisabled)
                    .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Medicine).Level).FirstOrDefault();
                doctor?.skills.GetSkill(SkillDefOf.Medicine).Learn(500f, true);
                outcome = VarietyUtil.Line(StorytellerEvents.More(def)?.outcomes, ctx, new[] { Kind(limb.defName) }) ?? outcome;
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== A merchant's venture =====================

    /// <summary>
    /// A merchant raising silver for a caravan venture asks the colony to put some in, and promises twice as much back
    /// in about ten days. It may pay, the caravan may be lost, or it may be a con: the colony's best talker (or an old
    /// trader who knows the merchant) makes a con less likely. Settled when the silver is handed over, told when due.
    /// </summary>
    public class HappeningWorker_Venture : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && map.resourceCounter.Silver >= 80;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly(VarietyUtil.OnGoodTerms) ?? Arrivals.Friendly();
            if (ctx.faction == null) return false;
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            // An old trader in the colony knows the merchant from before.
            ctx.a = adults.Where(p => Pasts.Has(p, Past.Trader)).RandomElementWithFallback() ?? VarietyUtil.Best(ctx.map, SkillDefOf.Social) ?? adults.RandomElementWithFallback();
            if (ctx.a == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Social, new IntRange(8, 14));
            ctx.count = StorytellerEvents.Silver(ctx.map, ext.value);
            ctx.tale = (ctx.count / 2).ToString();
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        private static bool OldTrader(Pawn a) => a != null && Pasts.Has(a, Past.Trader);

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (OldTrader(ctx.a)) tags.Add("trader");
        }

        protected override string Extra(HappeningContext ctx) =>
            VarietyUtil.Line(StorytellerEvents.More(def)?.extra, ctx, null, ctx.map.resourceCounter.Silver.ToString());

        private static int Stake(HappeningContext ctx, int option) => option == 0 ? ctx.count : ctx.count / 2;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && VarietyUtil.InStores(ctx.map, ThingDefOf.Silver) < Stake(ctx, option)
                ? "CC_Dilemma_NotEnough".Translate(Stake(ctx, option), ThingDefOf.Silver.label).ToString()
                : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option <= 1 && !byDefault && ctx.stranger != null && Couriers.TakeFromStoresFor(ctx, ThingDefOf.Silver, Stake(ctx, option)))
            {
                // How it ends is settled now, and told when it's due.
                int social = ctx.a?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
                float con = Mathf.Clamp(0.3f - social * 0.015f - (OldTrader(ctx.a) ? 0.1f : 0f), 0.04f, 0.3f);
                float roll = Rand.Value;
                string fate = roll < con ? "con" : roll < con + 0.2f ? "lost" : "good";
                var m = StorytellerEvents.Schedule(StorytellerEvents.Venture, null, ctx.faction, fate, ctx.stranger.LabelShort, ctx.a, Rand.Range(8f, 12f));
                if (m != null) m.amount = Stake(ctx, option);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>The venture comes home: twice the silver, a little of it, or nobody at all.</summary>
        public static void Return(Memo m)
        {
            var def = StorytellerEvents.EventOf(m.kind);
            var map = HappeningTracker.HomeMap();
            if (def == null || map == null || m.amount <= 0) return;
            var more = StorytellerEvents.More(def);
            var ctx = StorytellerEvents.Context(m, def, map);
            string fate = m.def ?? "good";
            // Their people are gone or at war with the colony: the caravan never comes back this way.
            if (fate == "good" && !LivingEvents.Friendly(m.faction)) fate = "lost";
            var spot = DropCellFinder.TradeDropSpot(map);
            if (fate == "good")
            {
                ctx.count = Mathf.RoundToInt(m.amount * Rand.Range(1.7f, 2.3f) / 5f) * 5;
                StorytellerEvents.Give(m.other, "Council_Evt_VentureWon", def);
                // Their carters bring it home, and the letter comes with them (Couriers/); else it's at the trade spot now.
                if (!Couriers.BringHome(map, Couriers.Make(ThingDefOf.Silver, ctx.count), m.faction, spot, ctx, more?.doneTitles, more?.done, new[] { "good" }, LetterDefOf.PositiveEvent))
                {
                    VarietyUtil.PaySilver(map, spot, ctx.count);
                    LivingEvents.Tell(ctx, more?.doneTitles, more?.done, new[] { "good" }, LetterDefOf.PositiveEvent, new LookTargets(new TargetInfo(spot, map)));
                }
            }
            else if (fate == "lost")
            {
                ctx.count = Mathf.Max(5, Mathf.RoundToInt(m.amount * Rand.Range(0.2f, 0.4f) / 5f) * 5);
                if (!Couriers.BringHome(map, Couriers.Make(ThingDefOf.Silver, ctx.count), m.faction, spot, ctx, more?.doneTitles, more?.done, new[] { "lost" }, LetterDefOf.NeutralEvent))
                {
                    VarietyUtil.PaySilver(map, spot, ctx.count);
                    LivingEvents.Tell(ctx, more?.doneTitles, more?.done, new[] { "lost" }, LetterDefOf.NeutralEvent, new LookTargets(new TargetInfo(spot, map)));
                }
            }
            else
            {
                StorytellerEvents.Give(m.other, "Council_Evt_Conned", def);
                LivingEvents.Tell(ctx, more?.failTitles, more?.fail, null, LetterDefOf.NegativeEvent, LookTargets.Invalid);
            }
        }
    }

    // ===================== A prisoner someone wants =====================

    /// <summary>
    /// A neighbor learns the colony holds someone who wronged them (a raider from a hostile people) and offers silver
    /// to have them handed over. Or the colony releases the prisoner instead, or keeps them.
    /// </summary>
    public class HappeningWorker_WantedPrisoner : IncidentWorker_Happening
    {
        private static bool Wanted(Pawn p) =>
            p != null && !p.Dead && p.Spawned && p.IsPrisonerOfColony && p.RaceProps.Humanlike && !p.IsSlave && p.guest != null && p.Faction != null
            && !p.Faction.IsPlayer && !p.Faction.Hidden && p.Faction.HostileTo(Faction.OfPlayer) && !p.IsQuestLodger() && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p);

        private static IEnumerable<Pawn> Prisoners(Map map) => map.mapPawns.PrisonersOfColonySpawned.Where(Wanted);

        private static Faction Buyer(Faction not) => Arrivals.Friendly(f => f != not && VarietyUtil.OnGoodTerms(f));

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            StorytellerEvents.Adults(map).Any() && Prisoners(map).Any(p => Buyer(p.Faction) != null);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.stranger = Prisoners(ctx.map).Where(p => Buyer(p.Faction) != null).RandomElementWithFallback();
            if (ctx.stranger == null) return false;
            ctx.enemy = ctx.stranger.Faction;
            ctx.faction = Buyer(ctx.enemy);
            ctx.a = StorytellerEvents.BestOrAny(ctx.map, SkillDefOf.Social);
            ctx.count = StorytellerEvents.Silver(ctx.map, ext.value);
            return ctx.faction != null && ctx.a != null;
        }

        /// <summary>The prisoner was here before the event: never thrown away.</summary>
        protected override void Abort(HappeningContext ctx) => ctx.stranger = null;

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && !Wanted(ctx.stranger) ? "CC_St_NotPrisoner".Translate(ctx.stranger?.LabelShort ?? "?").ToString() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var p = ctx.stranger;
            if (option == 0 && !byDefault && Wanted(p))
            {
                string name = p.LabelShort;
                // The neighbor's riders come for them and pay once they have them (Couriers/); else out of the colony's hands
                // at once, as before.
                if (!Couriers.Fetch(ctx.map, p, ctx.faction, Couriers.Make(ThingDefOf.Silver, ctx.count), VarietyUtil.Near(ctx.a, ctx.map), true))
                {
                    VarietyUtil.PaySilver(ctx.map, VarietyUtil.Near(ctx.a, ctx.map), ctx.count);
                    // Taken away by the neighbor's riders: out of the colony's hands for good.
                    p.DeSpawn();
                    p.guest?.SetGuestStatus(null);
                    if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Decide);
                }
                var m = CouncilGameComponent.Instance?.happenings.Remember(StorytellerEvents.HandedOver, null, ctx.faction, null, name, false, ctx.a);
                if (m != null && ctx.enemy != null) m.amount = ctx.enemy.loadID;
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>The prisoner is the colony's: nothing to let go of.</summary>
        public override void Cleanup(HappeningContext ctx)
        {
        }
    }

    // ===================== A wounded stranger =====================

    /// <summary>
    /// A colonist finds a wounded stranger out on the land, armed, who won't say how they were hurt. Bring them in (and
    /// whoever hurt them may come looking), leave them food and bandages (they may remember it), or leave them be.
    /// </summary>
    public class HappeningWorker_WoundedStranger : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => StorytellerEvents.Adults(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.a = adults.Where(p => !p.Position.Roofed(ctx.map)).RandomElementWithFallback() ?? adults.RandomElementWithFallback();
            if (ctx.a == null) return false;
            var x = Arrivals.Make(Arrivals.JoinerKind(), null, ext.ages);
            if (x == null) return false;
            StorytellerEvents.Skilled(x, SkillDefOf.Shooting, new IntRange(5, 10));
            StorytellerEvents.Skilled(x, SkillDefOf.Melee, new IntRange(5, 10));
            ctx.stranger = x;
            ctx.xWaiting = true;
            Arrivals.Hold(x);
            // Whoever may be after them (nobody, if no one would come).
            ctx.enemy = Raids.Enemy();
            return true;
        }

        public override float RiskChance(HappeningContext ctx, int option, float chance) =>
            option == 0 && ctx.enemy == null ? 0f : base.RiskChance(ctx, option, chance);

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var x = ctx.stranger;
            if (option == 0 && x != null && !x.Dead && !x.Spawned && ctx.map != null)
            {
                // Carried in from where they were found, still too weak to stand.
                var near = VarietyUtil.Near(ctx.a, ctx.map);
                if (Arrivals.Spawn(x, ctx.map, near.IsValid ? near : (IntVec3?)null))
                {
                    HealthUtility.DamageUntilDowned(x, false);
                    if (!x.Dead && Arrivals.Join(x, ctx.map))
                    {
                        ctx.xWaiting = false;
                        StorytellerEvents.Give(x, "Council_Evt_Sheltered", def);
                    }
                }
            }
            else if (option == 1 && x != null)
            {
                StorytellerEvents.Schedule(StorytellerEvents.Mended, null, null, Rand.Chance(0.55f) ? "gift" : "none", x.LabelShort, ctx.a, Rand.Range(5f, 12f));
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>Days later, maybe a bundle at the gate with thanks. Maybe nothing at all.</summary>
        public static void Repaid(Memo m)
        {
            if (m.def != "gift") return;
            var def = StorytellerEvents.EventOf(m.kind);
            var map = HappeningTracker.HomeMap();
            if (def == null || map == null) return;
            var ctx = StorytellerEvents.Context(m, def, map);
            var things = Loot.Make(Rand.Range(120f, 300f) * LivingEvents.WealthFactor(map));
            if (things.Count == 0) return;
            var spot = DropCellFinder.TradeDropSpot(map);
            var more = StorytellerEvents.More(def);
            string what = Loot.Describe(things);
            // Brought to the gate by one of theirs, the letter with them (Couriers/); else there at once.
            if (Couriers.BringHome(map, things, m.faction, spot, ctx, more?.doneTitles, more?.done, null, LetterDefOf.PositiveEvent, what)) return;
            Loot.Deliver(things, map, spot, false);
            LivingEvents.Tell(ctx, more?.doneTitles, more?.done, null, LetterDefOf.PositiveEvent, new LookTargets(new TargetInfo(spot, map)), what);
        }
    }

    // ===================== Recognized =====================

    /// <summary>
    /// A traveler recognizes a colonist with an outlaw past from a poster, and wants silver to keep quiet. Pay, call the
    /// bluff (they may talk to their people), or let the colonist talk to them.
    /// </summary>
    public class HappeningWorker_Recognized : HappeningWorker_Visitors
    {
        private static IEnumerable<Pawn> Outlaws(Map map) => StorytellerEvents.Adults(map).Where(p => Pasts.Has(p, Past.Outlaw));

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Outlaws(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Outlaws(ctx.map).RandomElementWithFallback();
            ctx.faction = Arrivals.Friendly();
            if (ctx.a == null || ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            ctx.tale = StorytellerEvents.WhoTheyWere(ctx.a);
            ctx.count = StorytellerEvents.Silver(ctx.map, ext.value);
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            // They know now, whatever was decided: a later event may bring them back.
            if (ctx.stranger != null && ctx.a != null && !(option == 2 && !risk))
                CouncilGameComponent.Instance?.happenings.Remember(StorytellerEvents.Recognized, ctx.stranger, ctx.faction, null, null, option == 0 || !risk, ctx.a);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== An old commander's call =====================

    /// <summary>
    /// A colonist's old commander now leads a neighbor's fighters against a hostile people, and asks for supplies. Sent,
    /// word comes back in a week or so: a share of the spoils if the campaign went well, thanks if it didn't. Not with
    /// Factional War, which has the neighbors' wars of its own.
    /// </summary>
    public class HappeningWorker_OldCommander : IncidentWorker_Happening
    {
        private const string FactionalWar = "sr.modrimworld.factionalwarcontinued";

        private static IEnumerable<Pawn> Soldiers(Map map) => StorytellerEvents.Adults(map).Where(p => Pasts.Has(p, Past.Soldier));

        /// <summary>What the colony could send: medicine, else food, else silver (and how much).</summary>
        private static bool Supplies(Map map, out ThingDef thing, out int count)
        {
            var med = map.resourceCounter.AllCountedAmounts.Where(kv => kv.Key.IsMedicine && kv.Value >= 8).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
            if (med != null)
            {
                thing = med;
                count = Rand.RangeInclusive(4, 7);
                return true;
            }
            var food = map.resourceCounter.AllCountedAmounts.Where(kv => kv.Value >= 80 && kv.Key.IsNutritionGivingIngestible && !kv.Key.IsDrug && !kv.Key.IsCorpse
                                                                        && kv.Key.ingestible.HumanEdible && kv.Key.ingestible.preferability >= FoodPreferability.RawBad
                                                                        && kv.Key.BaseMarketValue <= 5f)
                .OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
            if (food != null)
            {
                thing = food;
                count = VarietyUtil.Round(Rand.RangeInclusive(50, 80));
                return true;
            }
            thing = ThingDefOf.Silver;
            count = VarietyUtil.Round(Rand.RangeInclusive(100, 160));
            return map.resourceCounter.Silver >= count;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !ModsConfig.IsActive(FactionalWar) && !People.ModActive(FactionalWar) && Soldiers(map).Any()
            && Arrivals.Friendly(VarietyUtil.OnGoodTerms) is Faction f && Raids.Enemy(e => e != f) != null && Supplies(map, out _, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Soldiers(ctx.map).RandomElementWithFallback();
            ctx.faction = Arrivals.Friendly(VarietyUtil.OnGoodTerms);
            if (ctx.a == null || ctx.faction == null) return false;
            ctx.enemy = Raids.Enemy(e => e != ctx.faction);
            if (ctx.enemy == null || !Supplies(ctx.map, out var thing, out int count)) return false;
            ctx.thing = thing;
            ctx.variant = count;
            ctx.tale = VarietyUtil.Bundle(thing, count);
            // The commander: out in the world, written about, never seen.
            var kind = Arrivals.PersonKind(ctx.faction);
            var x = kind == null ? null : Arrivals.Make(kind, ctx.faction, ext.ages);
            if (x == null) return false;
            ctx.stranger = x;
            ctx.xWaiting = true;
            Arrivals.Hold(x);
            return true;
        }

        protected override string Extra(HappeningContext ctx) =>
            VarietyUtil.Line(StorytellerEvents.More(def)?.extra, ctx, null, VarietyUtil.Bundle(ctx.thing, VarietyUtil.InStores(ctx.map, ctx.thing)));

        public override string Unavailable(HappeningContext ctx, int option) =>
            option == 0 && ctx.thing != null && VarietyUtil.InStores(ctx.map, ctx.thing) < ctx.variant
                ? "CC_Dilemma_NotEnough".Translate(ctx.variant, ctx.thing.label).ToString()
                : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option == 0 && !byDefault && ctx.thing != null && ctx.stranger != null && ctx.enemy != null && Couriers.TakeFromStoresFor(ctx, ctx.thing, ctx.variant))
            {
                var m = StorytellerEvents.Schedule(StorytellerEvents.Campaign, null, ctx.faction, ctx.thing.defName, ctx.stranger.LabelShort, ctx.a, Rand.Range(7f, 12f));
                if (m != null)
                {
                    m.amount = ctx.variant;
                    m.times = ctx.enemy.loadID;
                }
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>Word from the campaign: spoils and thanks, or thanks alone.</summary>
        public static void Report(Memo m)
        {
            var def = StorytellerEvents.EventOf(m.kind);
            var map = HappeningTracker.HomeMap();
            if (def == null || map == null) return;
            var more = StorytellerEvents.More(def);
            var ctx = StorytellerEvents.Context(m, def, map);
            if (!LivingEvents.Friendly(m.faction))
            {
                LivingEvents.Say(ctx, more?.cancelled);
                return;
            }
            bool won = ctx.enemy == null || ctx.enemy.defeated || Rand.Chance(0.6f);
            var spot = DropCellFinder.TradeDropSpot(map);
            if (won)
            {
                float sent = Mathf.Max(150f, (ctx.thing?.BaseMarketValue ?? 1f) * m.amount);
                var things = Loot.Make(sent * Rand.Range(1.5f, 2.5f));
                string what = Loot.Describe(things);
                if (m.faction.HasGoodwill) m.faction.TryAffectGoodwillWith(Faction.OfPlayer, 8, true, false);
                StorytellerEvents.Give(m.other, "Council_Evt_OldUnitWon", def);
                // The colony's share is carried in, the letter with it (Couriers/); else there at once.
                if (!Couriers.BringHome(map, things, m.faction, spot, ctx, more?.doneTitles, more?.done, new[] { "won" }, LetterDefOf.PositiveEvent, what))
                {
                    Loot.Deliver(things, map, spot, false);
                    LivingEvents.Tell(ctx, more?.doneTitles, more?.done, new[] { "won" }, LetterDefOf.PositiveEvent, new LookTargets(new TargetInfo(spot, map)), what);
                }
            }
            else
            {
                if (m.faction.HasGoodwill) m.faction.TryAffectGoodwillWith(Faction.OfPlayer, 3, true, false);
                StorytellerEvents.Give(m.other, "Council_Evt_OldUnitLost", def);
                LivingEvents.Tell(ctx, more?.doneTitles, more?.done, new[] { "lost" }, LetterDefOf.NeutralEvent, LookTargets.Invalid);
            }
        }
    }

    // ===================== A former patient =====================

    /// <summary>
    /// Someone a colonist healed long ago (a healer by trade, or the colony's doctor) comes to repay it: with a gift,
    /// by staying on to work (they may have family waiting), or by spreading the colonist's name among the neighbors.
    /// </summary>
    public class HappeningWorker_FormerPatient : HappeningWorker_Visitors
    {
        private static bool Healer(Pawn p) => Pasts.Has(p, Past.Healer);

        private static IEnumerable<Pawn> Healers(Map map) =>
            StorytellerEvents.Adults(map).Where(p => Healer(p) || (p.skills?.GetSkill(SkillDefOf.Medicine) is SkillRecord r && !r.TotallyDisabled && r.Level >= 10));

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Healers(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Healers(ctx.map).RandomElementByWeightWithFallback(p => Healer(p) ? 3f : 1f);
            ctx.faction = Arrivals.Friendly();
            if (ctx.a == null || ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            ctx.variant = Mathf.RoundToInt(ext.value.RandomInRange * LivingEvents.WealthFactor(ctx.map));
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(Healer(ctx.a) ? "healer" : "doctor");

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var x = ctx.stranger;
            var near = x != null && x.Spawned && x.Map == ctx.map ? x.Position : VarietyUtil.Near(ctx.a, ctx.map);
            // The gift: accepted, or left behind by someone who can't stay.
            if (option == 0 || (option == 1 && risk))
                Couriers.Loot(Loot.Make(ctx.variant), ctx.map, near, Couriers.PeopleOf(ctx), x);
            if (option == 2 && !byDefault)
                foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.HasGoodwill && !f.HostileTo(Faction.OfPlayer)))
                    f.TryAffectGoodwillWith(Faction.OfPlayer, 3, false, false);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== A crashed drone =====================

    /// <summary>
    /// Industrial and spacer colonies: a neighbor's survey drone comes down in the colony's fields. Strip it for parts,
    /// let the colony's cleverest pull its memory (research, if it doesn't wipe itself), or return it for a reward.
    /// </summary>
    public class HappeningWorker_CrashedDrone : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !Avail.IsPreIndustrial && Arrivals.Friendly() != null && StorytellerEvents.Adults(map).Any() && StorytellerEvents.TryOpenSpot(map, 12f, 50f, out _);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            ctx.a = StorytellerEvents.BestOrAny(ctx.map, SkillDefOf.Intellectual);
            ctx.count = StorytellerEvents.Silver(ctx.map, ext.value);
            return ctx.faction != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            if (!StorytellerEvents.TryOpenSpot(ctx.map, 12f, 50f, out var cell)) return false;
            ctx.place = GenSpawn.Spawn(ThingDefOf.ChunkSlagSteel, cell, ctx.map);
            ctx.spot = cell;
            ctx.placeName = StorytellerEvents.Direction(ctx.map, cell);
            for (int i = 0; i < 6; i++)
            {
                var c = cell + GenRadial.RadialPattern[Rand.RangeInclusive(1, 12)];
                if (c.InBounds(ctx.map)) FilthMaker.TryMakeFilth(c, ctx.map, ThingDefOf.Filth_MachineBits);
            }
            return true;
        }

        private static bool Wreck(HappeningContext ctx) => ctx.place != null && ctx.place.Spawned && ctx.place.Map == ctx.map;

        public override string Unavailable(HappeningContext ctx, int option) =>
            !Wreck(ctx) ? StorytellerEvents.Gone("CC_St_TheWreck".Translate()) : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (Wreck(ctx))
            {
                var at = ctx.place.Position;
                if (option == 0)
                {
                    StorytellerEvents.Hand(ThingDefOf.ComponentIndustrial, Rand.RangeInclusive(2, 4), ctx.map, at);
                    StorytellerEvents.Hand(ThingDefOf.Steel, Rand.RangeInclusive(20, 35), ctx.map, at);
                }
                else if (option == 1)
                    StorytellerEvents.Hand(ThingDefOf.ComponentIndustrial, risk ? 1 : 2, ctx.map, at);
                else if (option == 2 && !byDefault)
                {
                    // Their people come for the drone and pay for it on the spot (Couriers/); else gone and paid for at once.
                    if (!Couriers.CollectThings(ctx.map, new List<Thing> { ctx.place }, ctx.faction, Couriers.Make(ThingDefOf.Silver, ctx.count)))
                    {
                        VarietyUtil.PaySilver(ctx.map, at, ctx.count);
                        ctx.place.Destroy();
                    }
                }
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== A signal =====================

    /// <summary>
    /// Industrial and spacer colonies with a comms console or radio: a repeating signal. Answer it (a survivor in a pod
    /// who comes down to join, an old beacon, or a pirates' lure: a fight), trace it quietly (the colony's cleverest may
    /// find where it comes from, a cache out on the land), or leave it.
    /// </summary>
    public class HappeningWorker_Signal : IncidentWorker_Happening
    {
        private static bool Comms(Map map) =>
            map.listerBuildings.allBuildingsColonist.Any(b => b.def.defName == "CommsConsole" || b.def.defName.Contains("Radio"));

        protected override bool CanHappen(Map map, HappeningExtension ext) => !Avail.IsPreIndustrial && Comms(map) && StorytellerEvents.Adults(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = StorytellerEvents.BestOrAny(ctx.map, SkillDefOf.Intellectual);
            ctx.enemy = Raids.Enemy(f => f.def.techLevel >= TechLevel.Industrial) ?? Raids.Enemy();
            return ctx.a != null;
        }

        /// <summary>No lure without someone to lay it.</summary>
        public override float RiskChance(HappeningContext ctx, int option, float chance) =>
            option == 0 && ctx.enemy == null ? 0f : base.RiskChance(ctx, option, chance);

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var more = StorytellerEvents.More(def);
            if (option == 0 && !risk && !byDefault && ctx.map != null)
            {
                // A survivor more often than not; otherwise an old beacon still calling out to nobody.
                Pawn x = Rand.Chance(0.55f) ? Arrivals.Make(Arrivals.JoinerKind(), null, new FloatRange(20f, 50f)) : null;
                if (x != null)
                {
                    x.SetFaction(Faction.OfPlayer);
                    var drop = DropCellFinder.RandomDropSpot(ctx.map);
                    DropPodUtility.DropThingsNear(drop, ctx.map, new List<Thing> { x }, 110, false, false, true, false);
                    ctx.stranger = x;
                    CouncilGameComponent.Instance?.happenings.Remember(StorytellerEvents.Survivor, x, null, null, null, false, ctx.a);
                }
                outcome = VarietyUtil.Line(more?.outcomes, ctx, new[] { x != null ? "survivor" : "beacon" }) ?? outcome;
            }
            else if (option == 1 && !risk && !byDefault && ctx.map != null && StorytellerEvents.TryOpenSpot(ctx.map, 30f, 999f, out var cell))
            {
                var things = Loot.Make(Rand.Range(250f, 500f) * LivingEvents.WealthFactor(ctx.map), Rand.Chance(0.3f));
                foreach (var t in things) GenPlace.TryPlaceThing(t, cell, ctx.map, ThingPlaceMode.Near);
                ctx.placeName = StorytellerEvents.Direction(ctx.map, cell);
                LivingEvents.Tell(ctx, more?.doneTitles, more?.done, null, LetterDefOf.PositiveEvent, new LookTargets(new TargetInfo(cell, ctx.map)), Loot.Describe(things));
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The lord's tithe =====================

    /// <summary>
    /// Medieval colonies: the reeve of a neighboring lord says the colony's land owes a tithe. Pay in silver or in kind,
    /// let the colony's best talker dispute the claim (losing costs more), or refuse (the neighbor thinks less of the
    /// colony, never enough to go to war over it).
    /// </summary>
    public class HappeningWorker_Tithe : HappeningWorker_Visitors
    {
        private static bool Medieval => Avail.IsPreIndustrial && Avail.EffectiveTech >= TechLevel.Medieval;

        private static Faction Lord() =>
            Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && f.def.techLevel == TechLevel.Medieval) ?? Arrivals.Friendly(VarietyUtil.OnGoodTerms);

        protected override bool CanHappen(Map map, HappeningExtension ext) => Medieval && Lord() != null && StorytellerEvents.Adults(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Lord();
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            ctx.a = StorytellerEvents.BestOrAny(ctx.map, SkillDefOf.Social);
            ctx.count = StorytellerEvents.Silver(ctx.map, ext.value);
            ctx.variant = VarietyUtil.Round(Mathf.RoundToInt(ctx.count * 1.2f));
            ctx.tale = StorytellerEvents.FoodLabel(ctx.variant);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override string Extra(HappeningContext ctx) =>
            VarietyUtil.Line(StorytellerEvents.More(def)?.extra, ctx, null, ctx.map.resourceCounter.Silver + ", " + StorytellerEvents.FoodLabel(DilemmaEffect_Consume.RawFood(ctx.map)));

        public override string Unavailable(HappeningContext ctx, int option) => option == 1 ? StorytellerEvents.Food(ctx.variant).Unavailable(ctx) : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option == 1 && !byDefault && StorytellerEvents.Food(ctx.variant).Unavailable(ctx) == null) StorytellerEvents.Food(ctx.variant).Apply(ctx);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== An envoy =====================

    /// <summary>
    /// A hostile people sends an envoy (a pipe-bearer, a herald under a white flag, a negotiator) to offer peace at a
    /// price: silver, or food. Paid, they're at peace with the colony (their goodwill is lifted to just above zero, which
    /// ends the hostility). Never a people that can't make peace (pirates and the like). The envoy waits outside.
    /// </summary>
    public class HappeningWorker_Envoy : IncidentWorker_Happening
    {
        private static bool Fits(Faction f) =>
            f != null && !f.IsPlayer && !f.Hidden && !f.defeated && !f.temporary && f.def.humanlikeFaction && !f.def.permanentEnemy && f.HasGoodwill
            && f.HostileTo(Faction.OfPlayer) && Arrivals.PersonKind(f) != null && f.CanChangeGoodwillFor(Faction.OfPlayer, 50);

        private static Faction Pick() =>
            Find.FactionManager.AllFactionsListForReading.Where(Fits).RandomElementByWeightWithFallback(f => Arrivals.TechWeight(f, Avail.EffectiveTech));

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Pick() != null && StorytellerEvents.Adults(map).Any() && !MapFacts.ThreatOn(map)
            && Now - map.storyState.LastThreatBigTick > GenDate.TicksPerDay * 3;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.enemy = Pick();
            ctx.a = StorytellerEvents.BestOrAny(ctx.map, SkillDefOf.Social);
            if (ctx.enemy == null || ctx.a == null) return false;
            var x = Arrivals.Make(Arrivals.PersonKind(ctx.enemy), ctx.enemy, ext.ages);
            if (x == null) return false;
            ctx.stranger = x;
            ctx.xWaiting = true;
            Arrivals.Hold(x);
            // The deeper the bad blood, the higher the price.
            float grudge = Mathf.Clamp(-ctx.enemy.PlayerGoodwill, 10f, 100f);
            ctx.count = Mathf.Clamp(Mathf.RoundToInt(grudge * 6f * LivingEvents.WealthFactor(ctx.map) / 10f) * 10, 150, 900);
            ctx.variant = VarietyUtil.Round(Mathf.RoundToInt(ctx.count * 1.1f));
            ctx.tale = StorytellerEvents.FoodLabel(ctx.variant);
            return true;
        }

        protected override string Extra(HappeningContext ctx) =>
            VarietyUtil.Line(StorytellerEvents.More(def)?.extra, ctx, null, ctx.map.resourceCounter.Silver + ", " + StorytellerEvents.FoodLabel(DilemmaEffect_Consume.RawFood(ctx.map)));

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option <= 1 && !Fits(ctx.enemy)) return "CC_St_NoLongerAtWar".Translate(ctx.enemy?.Name ?? "?").ToString();
            return option == 1 ? StorytellerEvents.Food(ctx.variant).Unavailable(ctx) : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var e = ctx.enemy;
            if (option <= 1 && !byDefault && Fits(e))
            {
                // The silver or the food goes out with the answer's own errand (DilemmaEffect_CarryOut, carried to the
                // envoy at the gate; taken at once if it never happens).
                // Lifted to just above zero, where the game ends a war.
                e.TryAffectGoodwillWith(Faction.OfPlayer, Mathf.Max(5, 5 - e.PlayerGoodwill), true, false);
                if (!e.HostileTo(Faction.OfPlayer)) CouncilGameComponent.Instance?.happenings.Remember(StorytellerEvents.Peace, null, e, null, null, false, ctx.a);
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== An arcane tome (A RimWorld of Magic) =====================

    /// <summary>
    /// A RimWorld of Magic: a wandering scholar sells one of its class books (an arcane tome or a combat manual) for
    /// silver. A colonist with the gift for it is the one who wants it most.
    /// </summary>
    public class HappeningWorker_ArcaneTome : HappeningWorker_Visitors
    {
        public const string Mod = "torann.arimworldofmagic";

        private static List<ThingDef> tomes;

        private static List<ThingDef> Tomes => tomes ??= DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d.defName.StartsWith("BookOf") && d.category == ThingCategory.Item && d.BaseMarketValue > 0f && LivingEvents.FromMod(d, Mod)).ToList();

        /// <summary>Someone with the latent gift a class book needs (A RimWorld of Magic's traits, by name).</summary>
        private static bool Gifted(Pawn p) => p?.story?.traits?.allTraits?.Any(t => t.def.defName == "TM_Gifted" || t.def.defName == "PhysicalProdigy") == true;

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Tomes.Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            ctx.thing = Tomes.RandomElementWithFallback();
            if (ctx.faction == null || ctx.thing == null) return false;
            var adults = StorytellerEvents.Adults(ctx.map).ToList();
            ctx.a = adults.Where(Gifted).RandomElementWithFallback() ?? adults.RandomElementWithFallback();
            if (ctx.a == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            ctx.count = Mathf.Max(150, Mathf.RoundToInt(ctx.thing.BaseMarketValue * 0.6f / 10f) * 10);
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, GatheringSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(Gifted(ctx.a) ? "gifted" : "ungifted");

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option == 0 && !byDefault && ctx.thing != null)
                Couriers.Hand(ctx.thing, 1, ctx.map, VarietyUtil.Near(ctx.a, ctx.map), Couriers.PeopleOf(ctx), ctx.stranger);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }
}
