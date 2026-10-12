using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // A fourth batch of world events, for a colony with a past. Some pick up what earlier events left behind (an order
    // filled, runaway lovers taken in, a family visit, a bad fortune, a quarrel judged); some draw on what Rimfolk knows
    // about the colony (its epithets, vendettas, life goals, age and customs); some take days to play out (a caravan to a
    // far market, a harvest fair, a relative invited to stay, a guest who writes ahead); some come with the seasons; and
    // four come only with a mod the player runs (A RimWorld of Magic, Alpha Animals, Dubs Bad Hygiene, Hospitality).
    // One can end in a fight, and only if the colony chooses it.

    /// <summary>More text for the events below: everything in <see cref="HappeningMoreText"/>, and how a memory reads on the World page.</summary>
    public class HappeningLivingText : HappeningMoreText
    {
        /// <summary>A line under "who's out there" while the memory waits for the event that follows it up.</summary>
        [MustTranslate] public List<string> remembered;
    }

    /// <summary>
    /// What the events below share: the memories earlier events leave (memos), things on their way (memos with a due
    /// tick, shown on the World page), and the hourly round that brings them in.
    /// </summary>
    public static class LivingEvents
    {
        // Memories, kept until a later event picks them up or they fade (KeepDays).
        public const string Buyer = "buyer";
        public const string Eloped = "eloped";
        public const string Kin = "kin";
        public const string Unlucky = "unlucky";
        public const string Quarrel = "quarrel";

        // Things on their way: each memo has a due tick.
        public const string KinComing = "kincoming";
        public const string Market = "market";
        public const string Fair = "fair";
        public const string Guest = "guest";
        public const string Morning = "morning";

        /// <summary>The event whose text tells each kind of memo.</summary>
        private static readonly Dictionary<string, string> Events = new Dictionary<string, string>
        {
            { Buyer, "Council_Evt_RegularBuyer" }, { Eloped, "Council_Evt_LoversKin" }, { Kin, "Council_Evt_KinLetter" },
            { Unlucky, "Council_Evt_FortuneScare" }, { Quarrel, "Council_Evt_QuarrelRemembered" },
            { KinComing, "Council_Evt_KinLetter" }, { Market, "Council_Evt_FarMarket" }, { Fair, "Council_Evt_HarvestFair" },
            { Guest, "Council_Evt_NotableGuest" }, { Morning, "Council_Evt_MidwinterGuest" }
        };

        private static readonly HashSet<string> PendingKinds = new HashSet<string> { KinComing, Market, Fair, Guest, Morning };

        /// <summary>How long a memory waits for its follow-up, in days, before it fades.</summary>
        private static readonly Dictionary<string, float> KeepDays = new Dictionary<string, float>
        {
            { Buyer, 120f }, { Eloped, 120f }, { Kin, 150f }, { Unlucky, 25f }, { Quarrel, 120f }
        };

        private static int Now => Find.TickManager.TicksGame;

        private static HappeningTracker Tracker => CouncilGameComponent.Instance?.happenings;

        public static bool IsPending(Memo m) => m != null && !m.used && m.kind != null && PendingKinds.Contains(m.kind);

        public static IncidentDef EventOf(string kind) =>
            kind != null && Events.TryGetValue(kind, out var name) ? DefDatabase<IncidentDef>.GetNamedSilentFail(name) : null;

        public static HappeningLivingText More(IncidentDef def) => def?.GetModExtension<HappeningLivingText>();

        /// <summary>An unused memo of this kind about this people, or null.</summary>
        public static Memo MemoOf(string kind, Faction f) => Tracker?.memos.FirstOrDefault(m => m.kind == kind && !m.used && m.faction == f);

        public static Faction FactionById(int id) => Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.loadID == id);

        public static ThingDef ThingNamed(string defName) => defName.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(defName);

        /// <summary>A people that is still out there and not at war with the colony.</summary>
        public static bool Friendly(Faction f) => f != null && !f.IsPlayer && !f.defeated && !f.HostileTo(Faction.OfPlayer);

        public static bool AtHome(Pawn p, Map map) => p != null && !p.Dead && p.IsColonist && p.Spawned && p.Map == map;

        /// <summary>Someone out in the world who can travel now: not their people's leader, not held, not with a caravan, not wanted by a quest, nobody's nemesis.</summary>
        public static bool FreeToTravel(Pawn p)
        {
            if (p == null || p.Dead || p.Discarded || p.Spawned || !Find.WorldPawns.Contains(p)) return false;
            if (!Friendly(p.Faction) || p.Faction.leader == p || p.IsPrisoner || People.Kidnapped(p) || p.GetCaravan() != null || QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p)) return false;
            return !(CouncilGameComponent.Instance?.nemeses.IsNemesis(p) ?? false);
        }

        /// <summary>A def that comes from this mod (by package id, in lowercase; local copies count).</summary>
        public static bool FromMod(Def d, string packageId)
        {
            var pack = d?.modContentPack;
            string id = (pack?.PackageIdPlayerFacing ?? pack?.PackageId ?? "").ToLowerInvariant();
            return id.Length > 0 && (id == packageId || id.StartsWith(packageId + "_"));
        }

        /// <summary>Tiles between two places on the same layer of the world (a long way, for places on another layer).</summary>
        public static float Distance(PlanetTile a, PlanetTile b)
        {
            try
            {
                return a.Layer == b.Layer ? Find.WorldGrid.ApproxDistanceInTiles(a, b) : 9999f;
            }
            catch
            {
                return 9999f;
            }
        }

        /// <summary>The name of a people's town nearest the colony ("the fair at Kettleford"), or null.</summary>
        public static string TownOf(Faction f, Map map)
        {
            if (f == null || map == null) return null;
            Settlement best = null;
            float bestDist = float.MaxValue;
            foreach (var s in Find.WorldObjects.Settlements)
            {
                if (s.Faction != f || !s.HasName) continue;
                float d = Distance(map.Tile, s.Tile);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = s;
                }
            }
            return best?.Name;
        }

        /// <summary>A richer colony deals in larger amounts, within reason.</summary>
        public static float WealthFactor(Map map) => Mathf.Clamp(Mathf.Sqrt(Mathf.Max(1f, map?.wealthWatcher.WealthTotal ?? 40000f) / 40000f), 0.6f, 2f);

        // ---------------- What earlier events leave behind ----------------

        /// <summary>A neighbor's order was collected: filled, they may come back as regular buyers; not filled, they won't.</summary>
        public static void Notify_OrderCollected(Faction f, ThingDef goods, bool filled)
        {
            var t = Tracker;
            if (t == null || f == null || goods == null) return;
            var m = MemoOf(Buyer, f);
            if (!filled)
            {
                if (m != null) t.Use(m);
                return;
            }
            if (m == null)
            {
                m = t.Remember(Buyer, null, f, goods.defName);
                m.amount = 1;
            }
            else
            {
                m.amount++;
                m.def = goods.defName;
                m.tick = Now;
            }
            // No order out with them any more.
            m.times = 0;
        }

        /// <summary>Runaway lovers were taken in: their families know where they went.</summary>
        public static void Notify_Eloped(HappeningContext ctx)
        {
            var x = ctx?.stranger;
            var y = ctx?.group.FirstOrDefault();
            if (x == null || y == null || ctx.faction == null || x.Faction != Faction.OfPlayer || y.Faction != Faction.OfPlayer) return;
            Tracker?.Remember(Eloped, x, ctx.faction, null, null, false, y);
        }

        /// <summary>A relative visited and went home again: they may write.</summary>
        public static void Notify_FamilyVisited(HappeningContext ctx)
        {
            var x = ctx?.stranger;
            var t = Tracker;
            if (t == null || x == null || x.Dead || x.Faction == null || x.Faction == Faction.OfPlayer || ctx.a == null) return;
            // One letter-writer per colonist at a time.
            foreach (var old in t.memos.Where(m => m.kind == Kin && !m.used && m.other == ctx.a).ToList()) t.Use(old);
            t.Remember(Kin, x, x.Faction, null, ctx.relation, false, ctx.a);
        }

        /// <summary>The fortune teller saw something bad for them.</summary>
        public static void Notify_BadFortune(Pawn p)
        {
            var t = Tracker;
            if (t == null || p == null || t.memos.Any(m => m.kind == Unlucky && !m.used && m.other == p)) return;
            t.Remember(Unlucky, null, null, null, null, false, p);
        }

        /// <summary>
        /// The colony judged a quarrel between two neighbors (0: for the first, 1: for the second, 2: a middle way). The
        /// memo's faction is the side it favored (the first, for a middle way), its amount the other side's id.
        /// </summary>
        public static void Notify_Judged(HappeningContext ctx, int option, bool risk, bool byDefault)
        {
            // Staying out of it, or a middle way that fell apart, leaves nothing to remember.
            if (byDefault || ctx?.faction == null || ctx.enemy == null || option > 2 || (option == 2 && risk)) return;
            var t = Tracker;
            if (t == null) return;
            var favored = option == 1 ? ctx.enemy : ctx.faction;
            var other = option == 1 ? ctx.faction : ctx.enemy;
            var m = t.Remember(Quarrel, null, favored, option == 2 ? "middle" : "sided", null, false, ctx.a);
            m.amount = other.loadID;
        }

        /// <summary>Something on its way: comes due in <paramref name="days"/>. The memo pins its pawn in the world until then.</summary>
        public static Memo Schedule(string kind, Pawn pawn, Faction f, string def, string text, Pawn other, float days)
        {
            var t = Tracker;
            if (t == null) return null;
            // Whatever held them before (the letter that asked about them) lets go; the memo holds them now.
            if (pawn != null) Find.WorldPawns.ForcefullyKeptPawns.Remove(pawn);
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
                // No home to come back to right now (a gravship in flight, a caravan life): what's on its way waits.
                if (!home)
                {
                    m.due = now + GenDate.TicksPerHour;
                    continue;
                }
                // Used first, so one that throws can't come round every hour.
                t.Use(m);
                try
                {
                    Due(m);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("living " + m.kind, e);
                }
            }
            if (now % GenDate.TicksPerDay < GenDate.TicksPerHour) Fade(t, now);
            HappeningWorker_Founding.TryOnTheDay();
        }

        private static void Due(Memo m)
        {
            switch (m.kind)
            {
                case KinComing: HappeningWorker_KinLetter.Arrive(m); break;
                case Market: HappeningWorker_FarMarket.Return(m); break;
                case Fair: HappeningWorker_HarvestFair.Results(m); break;
                case Guest: HappeningWorker_NotableGuest.Arrive(m); break;
                case Morning: HappeningWorker_MidwinterGuest.Morning(m); break;
            }
        }

        /// <summary>Once a day: memories nobody followed up fade.</summary>
        private static void Fade(HappeningTracker t, int now)
        {
            foreach (var m in t.memos.Where(m => !m.used && m.kind != null && KeepDays.ContainsKey(m.kind)).ToList())
            {
                // Regular buyers waiting on an order that never came (their people turned on the colony meanwhile).
                if (m.kind == Buyer && m.times > 0 && !t.memos.Any(o => o.kind == "order" && !o.used && o.faction == m.faction)) m.times = 0;
                if (m.kind == Buyer && m.times > 0) continue;
                if (now - m.tick > KeepDays[m.kind] * GenDate.TicksPerDay) t.Use(m);
            }
        }

        // ---------------- Telling ----------------

        /// <summary>The people, things and place a memo is about, for its texts.</summary>
        public static HappeningContext Context(Memo m, IncidentDef def, Map map)
        {
            var ctx = new HappeningContext { incident = def, map = map, faction = m.faction, a = m.other, stranger = m.pawn };
            switch (m.kind)
            {
                case Kin:
                case KinComing:
                    ctx.relation = m.other != null && m.pawn != null ? m.other.GetMostImportantRelation(m.pawn)?.GetGenderSpecificLabel(m.pawn) ?? m.text : m.text;
                    break;
                case Market:
                {
                    var parts = (m.text ?? "").Split('|');
                    ctx.thing = ThingNamed(parts[0]);
                    ctx.count = m.times;
                    ctx.tale = VarietyUtil.Bundle(ThingNamed(m.def), m.amount);
                    ctx.placeName = parts.Length > 1 ? parts[1] : "";
                    break;
                }
                case Fair:
                    ctx.thing = ThingNamed(m.def);
                    ctx.variant = m.amount;
                    ctx.tale = VarietyUtil.Bundle(ctx.thing, m.amount);
                    ctx.placeName = m.text;
                    break;
                case Guest:
                    ctx.placeName = m.text;
                    break;
                case Buyer:
                    ctx.thing = ThingNamed(m.def);
                    ctx.count = m.amount;
                    break;
                case Eloped:
                    ctx.a = m.pawn;
                    ctx.b = m.other;
                    ctx.stranger = null;
                    break;
                case Quarrel:
                    ctx.enemy = FactionById(m.amount);
                    break;
            }
            return ctx;
        }

        /// <summary>A line that stays the same for as long as <paramref name="seed"/> does (the World page is drawn every frame).</summary>
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
            if (!IsPending(m) || m.kind == Morning) return null;
            var def = EventOf(m.kind);
            string line = Stable(More(def)?.coming, null, m.tick);
            if (line.NullOrEmpty()) return null;
            var ctx = Context(m, def, null);
            return ctx.Format(line.Replace("{DAYS}", TextUtil.DaysString(Mathf.Max(0, m.due - now))));
        }

        /// <summary>A memory waiting for its follow-up, for the World page's "who's out there".</summary>
        public static string MemoLine(Memo m)
        {
            if (m == null || m.used || m.kind == null) return null;
            var tags = new List<string>();
            switch (m.kind)
            {
                case Buyer:
                    if (!Friendly(m.faction) || ThingNamed(m.def) == null) return null;
                    tags.Add(m.amount >= 2 ? "again" : "once");
                    break;
                case Eloped:
                    if (m.pawn == null || m.pawn.Dead || m.other == null || m.other.Dead || m.faction == null || m.faction.defeated) return null;
                    break;
                case Kin:
                    if (m.pawn == null || m.pawn.Dead || m.other == null || m.other.Dead || !m.other.IsColonist) return null;
                    break;
                case Quarrel:
                    if (!Friendly(m.faction) || !Friendly(FactionById(m.amount))) return null;
                    tags.Add(m.def ?? "sided");
                    break;
                default:
                    return null;
            }
            var def = EventOf(m.kind);
            string line = Stable(More(def)?.remembered, tags, m.tick);
            return line.NullOrEmpty() ? null : Context(m, def, null).Format(line);
        }

        /// <summary>A letter about something that came of an earlier answer, and its first lines in the chronicle.</summary>
        public static void Tell(HappeningContext ctx, List<string> titles, List<string> texts, IEnumerable<string> tags, LetterDef letter, LookTargets look, string item = null)
        {
            string text = VarietyUtil.Line(texts, ctx, tags, item);
            if (text.NullOrEmpty()) return;
            string title = VarietyUtil.Line(titles, ctx, tags, item) ?? ctx.incident?.LabelCap.ToString() ?? "";
            Find.LetterStack.ReceiveLetter(title, text, letter ?? LetterDefOf.NeutralEvent, look ?? LookTargets.Invalid);
            int cut = text.IndexOf('\n');
            string first = cut > 0 ? text.Substring(0, cut) : text;
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, title + ": " + first, ctx.a != null && !ctx.a.Dead && ctx.a.IsColonist ? ctx.a : null));
        }

        /// <summary>A short message about something that came of an earlier answer.</summary>
        public static void Say(HappeningContext ctx, List<string> lines, IEnumerable<string> tags = null, string item = null, MessageTypeDef type = null, LookTargets look = null)
        {
            string text = VarietyUtil.Line(lines, ctx, tags, item);
            if (text.NullOrEmpty()) return;
            if (look != null && look.IsValid) Messages.Message(text, look, type ?? MessageTypeDefOf.NeutralEvent, false);
            else Messages.Message(text, type ?? MessageTypeDefOf.NeutralEvent, false);
        }

        /// <summary>Things handed over at the trade spot (or near someone), split into stacks.</summary>
        public static void Hand(ThingDef def, int count, Map map, IntVec3 near)
        {
            if (def == null || count <= 0 || map == null) return;
            var cell = near.IsValid && near.InBounds(map) ? near : DropCellFinder.TradeDropSpot(map);
            while (count > 0)
            {
                var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                thing.stackCount = Mathf.Min(count, Mathf.Max(1, def.stackLimit));
                count -= thing.stackCount;
                GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
            }
        }

        /// <summary>The event's own words for "caused by", for the moods it gives.</summary>
        public static string Cause(IncidentDef def) => def == null ? null : "CC_Cause_EventHappened".Translate(def.LabelCap).Resolve();
    }

    /// <summary>Goodwill with the event's people that never takes them below <see cref="floor"/>, so it can't turn them hostile.</summary>
    public class DilemmaEffect_FairGoodwill : DilemmaEffect
    {
        public int amount;
        public bool enemy;
        public int floor = -60;

        public override void Apply(DilemmaContext ctx)
        {
            var f = enemy ? (ctx as HappeningContext)?.enemy : ctx.faction ?? ctx.stranger?.Faction;
            if (f == null || f.IsPlayer || f.defeated || !f.HasGoodwill || f.HostileTo(Faction.OfPlayer)) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            int change = amount >= 0 ? amount : Mathf.Min(0, Mathf.Max(amount, floor - f.PlayerGoodwill));
            if (change != 0) f.TryAffectGoodwillWith(Faction.OfPlayer, change, true, false);
        }
    }

    // ===================== A regular buyer =====================

    /// <summary>
    /// A neighbor whose order the colony filled comes back for more of the same, with a small gift. Taken, it's an
    /// order like the first (collected in five days, shown on the World page); turned down, they stop asking.
    /// </summary>
    public class HappeningWorker_RegularBuyer : IncidentWorker_Happening
    {
        private static ThingDef Goods(Memo m) => LivingEvents.ThingNamed(m?.def);

        private static Memo Pick(Map map) =>
            CouncilGameComponent.Instance?.happenings.Memos(LivingEvents.Buyer, 15f)
                .Where(m => m.times == 0 && LivingEvents.Friendly(m.faction) && VarietyUtil.OnGoodTerms(m.faction) && Goods(m) is ThingDef d && VarietyUtil.InStores(map, d) >= 10)
                .RandomElementWithFallback();

        private static bool OrderOut => CouncilGameComponent.Instance?.happenings.Memos("order").Any() ?? false;

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !OrderOut && Pick(map) != null && Colonists(map).Any(p => !p.DevelopmentalStage.Juvenile());

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var m = Pick(ctx.map);
            var goods = Goods(m);
            if (m == null || goods == null) return false;
            int have = VarietyUtil.InStores(ctx.map, goods);
            // A little more than last time's sort of order: at least ten, at most twice what the stores hold now.
            float target = Rand.Range(300f, 650f) * LivingEvents.WealthFactor(ctx.map);
            int n = VarietyUtil.Round(Mathf.Clamp(Mathf.RoundToInt(target / Mathf.Max(0.1f, goods.BaseMarketValue)), 10, have * 2));
            ctx.faction = m.faction;
            ctx.thing = goods;
            ctx.variant = n;
            ctx.count = HappeningWorker_Order.Price(goods, n);
            ctx.tale = VarietyUtil.Bundle(goods, n);
            ctx.a = VarietyUtil.Best(ctx.map, SkillDefOf.Social) ?? Colonists(ctx.map).FirstOrDefault(p => !p.DevelopmentalStage.Juvenile());
            return ctx.a != null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var m = LivingEvents.MemoOf(LivingEvents.Buyer, ctx.faction);
            tags.Add(m != null && m.amount >= 2 ? "again" : "once");
        }

        protected override string Extra(HappeningContext ctx) =>
            VarietyUtil.Line(VarietyUtil.More(def)?.extra, ctx, null, VarietyUtil.Bundle(ctx.thing, VarietyUtil.InStores(ctx.map, ctx.thing)));

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            var buyer = LivingEvents.MemoOf(LivingEvents.Buyer, ctx.faction);
            if (t != null && option == 0 && ctx.thing != null && ctx.variant > 0 && LivingEvents.Friendly(ctx.faction))
            {
                // An order like the first one: the neighbor's order event collects it.
                var m = t.Remember("order", null, ctx.faction, ctx.thing.defName, null, false, ctx.a);
                m.amount = ctx.variant;
                m.due = Find.TickManager.TicksGame + HappeningWorker_Order.DueDays * GenDate.TicksPerDay;
                if (buyer != null) buyer.times = 1;
            }
            else if (t != null && buyer != null) t.Use(buyer);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== The lovers' family =====================

    /// <summary>
    /// A parent of one of the runaway lovers the colony took in comes asking after them. Let them talk (it may go
    /// well or badly), send a gift home to smooth things over, or ask them to leave. Nobody fights.
    /// </summary>
    public class HappeningWorker_LoversKin : HappeningWorker_Visitors
    {
        private Memo chosen;
        private bool made;

        private static Memo Pick(Map map) =>
            CouncilGameComponent.Instance?.happenings.Memos(LivingEvents.Eloped, 12f)
                .Where(m => LivingEvents.Friendly(m.faction) && m.faction.HasGoodwill && m.faction.PlayerGoodwill > -50 && Arrivals.PersonKind(m.faction) != null
                            && LivingEvents.AtHome(m.pawn, map) && LivingEvents.AtHome(m.other, map))
                .RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) => Pick(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var m = Pick(ctx.map);
            if (m == null) return false;
            ctx.faction = m.faction;
            foreach (var lover in new[] { m.pawn, m.other }.InRandomOrder())
            {
                var parent = Parent(ctx, lover, ext);
                if (parent == null) continue;
                ctx.a = lover;
                ctx.b = lover == m.pawn ? m.other : m.pawn;
                ctx.stranger = parent;
                break;
            }
            if (ctx.stranger == null) return false;
            ctx.relation = ctx.a.GetMostImportantRelation(ctx.stranger)?.GetGenderSpecificLabel(ctx.stranger);
            if (ctx.relation.NullOrEmpty())
            {
                Abort(ctx);
                return false;
            }
            ctx.count = Mathf.Clamp(VarietyUtil.Round(Mathf.RoundToInt(Rand.Range(60f, 120f) * LivingEvents.WealthFactor(ctx.map))), 40, 250);
            chosen = m;
            return true;
        }

        /// <summary>A parent of theirs among their people: one they already have who can travel, or the one nobody had met yet.</summary>
        private Pawn Parent(HappeningContext ctx, Pawn lover, HappeningExtension ext)
        {
            made = false;
            if (lover?.relations == null || lover.ageTracker == null) return null;
            var father = lover.GetFather();
            var mother = lover.GetMother();
            foreach (var p in new[] { father, mother })
                if (p != null && p.Faction == ctx.faction && LivingEvents.FreeToTravel(p) && !p.DevelopmentalStage.Juvenile()) return p;
            if (father != null && mother != null) return null;
            Gender g = father != null ? Gender.Female : mother != null ? Gender.Male : Rand.Bool ? Gender.Male : Gender.Female;
            float age = lover.ageTracker.AgeBiologicalYearsFloat;
            var range = new FloatRange(age + 18f, Mathf.Min(age + 38f, 80f));
            if (range.max < range.min) return null;
            string last = g == Gender.Male ? (lover.Name as NameTriple)?.Last : null;
            var x = Arrivals.Make(Arrivals.PersonKind(ctx.faction), ctx.faction, range, g, last, false, lover);
            if (x == null) return null;
            lover.relations.AddDirectRelation(PawnRelationDefOf.Parent, x);
            made = true;
            return x;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            bool ok = Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;
            // On failure Abort still needs to know whether this parent was made for the visit, to unmake them.
            if (!ok) return false;
            CouncilGameComponent.Instance?.happenings.Use(chosen);
            chosen = null;
            made = false;
            return true;
        }

        /// <summary>A parent made for this visit is unmade; one who existed before goes home.</summary>
        protected override void Abort(HappeningContext ctx)
        {
            var x = ctx.stranger;
            if (made && x != null && !x.Spawned)
            {
                ctx.a?.relations?.TryRemoveDirectRelation(PawnRelationDefOf.Parent, x);
                Arrivals.Discard(x);
            }
            else if (x != null && x.Spawned) Arrivals.SendAway(new[] { x });
            made = false;
            chosen = null;
            ctx.stranger = null;
            ctx.group.Clear();
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.stranger != null) tags.Add(ctx.stranger.gender == Gender.Female ? "mother" : "father");
        }
    }

    // ===================== A letter from home =====================

    /// <summary>
    /// The relative who visited writes, with news from home and a small parcel. The colony can write back, or invite
    /// them to come and live here: then they're on their way (shown on the World page), and arrive a few days later,
    /// unless something keeps them at home after all.
    /// </summary>
    public class HappeningWorker_KinLetter : IncidentWorker_Happening
    {
        private Memo chosen;

        private static Memo Pick(Map map) =>
            CouncilGameComponent.Instance?.happenings.Memos(LivingEvents.Kin, 20f)
                .Where(m => LivingEvents.FreeToTravel(m.pawn) && !m.pawn.DevelopmentalStage.Juvenile() && (m.pawn.royalty == null || m.pawn.royalty.AllTitlesForReading.Count == 0)
                            && LivingEvents.AtHome(m.other, map) && !m.other.DevelopmentalStage.Baby())
                .RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) => Pick(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var m = Pick(ctx.map);
            if (m == null) return false;
            ctx.a = m.other;
            ctx.stranger = m.pawn;
            ctx.faction = m.pawn.Faction;
            ctx.relation = ctx.a.GetMostImportantRelation(m.pawn)?.GetGenderSpecificLabel(m.pawn) ?? m.text;
            chosen = m;
            return !ctx.relation.NullOrEmpty();
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            CouncilGameComponent.Instance?.happenings.Use(chosen);
            chosen = null;
            // Kept in the world while the colony writes back.
            Arrivals.Hold(ctx.stranger);
            ctx.xWaiting = true;
            return true;
        }

        /// <summary>They were someone before this letter: never thrown away.</summary>
        protected override void Abort(HappeningContext ctx)
        {
            chosen = null;
            ctx.stranger = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.stranger != null) tags.Add(ctx.stranger.gender == Gender.Female ? "female" : "male");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option == 1 && !byDefault && ctx.stranger != null && ctx.a != null && !ctx.stranger.Dead)
                LivingEvents.Schedule(LivingEvents.KinComing, ctx.stranger, ctx.faction, null, ctx.relation, ctx.a, Rand.Range(3f, 6f));
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>Invited: the memo holds them now. Otherwise back to their own life.</summary>
        public override void Cleanup(HappeningContext ctx)
        {
            var x = ctx.stranger;
            if (x != null && (CouncilGameComponent.Instance?.happenings.Holds(x) ?? false)) return;
            base.Cleanup(ctx);
        }

        /// <summary>The invited relative is due: they walk in and stay, or write that they can't leave after all.</summary>
        public static void Arrive(Memo m)
        {
            var def = LivingEvents.EventOf(LivingEvents.KinComing);
            var more = LivingEvents.More(def);
            var map = HappeningTracker.HomeMap();
            if (def == null || map == null) return;
            var ctx = LivingEvents.Context(m, def, map);
            var x = m.pawn;
            var a = m.other;
            if (!LivingEvents.FreeToTravel(x) || a == null || a.Dead || !a.IsColonist)
            {
                LivingEvents.Say(ctx, more?.cancelled);
                return;
            }
            // Not everyone can leave home: a sick parent, a harvest to bring in, a grandchild on the way.
            if (Rand.Chance(0.25f) || !Arrivals.Join(x, map))
            {
                LivingEvents.Tell(ctx, more?.failTitles, more?.fail, null, LetterDefOf.NeutralEvent, a.Spawned ? new LookTargets(a) : null);
                return;
            }
            var moved = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_KinMovedIn");
            using (Causes.Push(LivingEvents.Cause(def)))
            {
                CouncilGameComponent.GiveThought(a, moved, x);
                CouncilGameComponent.GiveThought(x, moved, a);
            }
            LivingEvents.Tell(ctx, more?.doneTitles, more?.done, null, LetterDefOf.PositiveEvent, new LookTargets(x));
        }
    }

    // ===================== The fortune, come and gone =====================

    /// <summary>
    /// Someone the fortune teller saw bad things for has a fright that turns out to be nothing: nobody is hurt, and the
    /// bad fortune is spent.
    /// </summary>
    public class HappeningWorker_FortuneScare : IncidentWorker_Happening
    {
        private Memo chosen;

        private static Memo Pick(Map map) =>
            CouncilGameComponent.Instance?.happenings.Memos(LivingEvents.Unlucky, 2f)
                .Where(m => LivingEvents.AtHome(m.other, map) && !m.other.Downed && !m.other.InMentalState && !m.other.DevelopmentalStage.Baby())
                .RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) => Pick(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            chosen = Pick(ctx.map);
            ctx.a = chosen?.other;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            CouncilGameComponent.Instance?.happenings.Use(chosen);
            chosen = null;
            var bad = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_BadFortune");
            if (bad != null) ctx.a.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(bad);
            return true;
        }

        protected override void Abort(HappeningContext ctx) => chosen = null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.a?.DevelopmentalStage.Child() == true) tags.Add("child");
        }
    }

    // ===================== A quarrel remembered =====================

    /// <summary>
    /// The neighbors whose quarrel the colony judged remember it: the side it favored sends something, or, when the
    /// colony found a middle way that held, both do (and whoever worked it out hears about it).
    /// </summary>
    public class HappeningWorker_QuarrelRemembered : IncidentWorker_Happening
    {
        private Memo chosen;

        private static Memo Pick() =>
            CouncilGameComponent.Instance?.happenings.Memos(LivingEvents.Quarrel, 25f)
                .Where(m => LivingEvents.Friendly(m.faction) && LivingEvents.FactionById(m.amount) is Faction o && o != m.faction && LivingEvents.Friendly(o))
                .RandomElementWithFallback();

        protected override bool CanHappen(Map map, HappeningExtension ext) => Pick() != null && Colonists(map).Any(p => !p.DevelopmentalStage.Juvenile());

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var m = Pick();
            if (m == null) return false;
            ctx.faction = m.faction;
            ctx.enemy = LivingEvents.FactionById(m.amount);
            ctx.variant = m.def == "middle" ? 1 : 0;
            ctx.a = LivingEvents.AtHome(m.other, ctx.map) ? m.other : Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            ctx.b = m.other;
            chosen = m;
            return ctx.a != null && ctx.enemy != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var things = Loot.Make(Rand.Range(150f, 350f) * LivingEvents.WealthFactor(map));
            if (things.Count == 0) return false;
            Loot.Deliver(things, map, VarietyUtil.Near(ctx.a, map), true);
            ctx.tale = Loot.Describe(things);
            foreach (var f in ctx.variant == 1 ? new[] { ctx.faction, ctx.enemy } : new[] { ctx.faction })
                if (f != null && f.HasGoodwill && !f.defeated) f.TryAffectGoodwillWith(Faction.OfPlayer, 4, true, false);
            // Whoever found the middle way hears it held.
            if (ctx.variant == 1 && ctx.a == ctx.b)
            {
                using (Causes.Push(LivingEvents.Cause(def)))
                    CouncilGameComponent.GiveThought(ctx.a, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_SettlementHeld"));
            }
            CouncilGameComponent.Instance?.happenings.Use(chosen);
            chosen = null;
            return true;
        }

        protected override void Abort(HappeningContext ctx) => chosen = null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(ctx.variant == 1 ? "middle" : "sided");
            if (ctx.variant == 1 && ctx.a != null && ctx.a == ctx.b) tags.Add("mediator");
        }
    }

    // ===================== Known by name =====================

    /// <summary>
    /// Someone has heard of a colonist by the name the colony gave them (the Healer, Greenhand, the Builder) and comes
    /// to ask for their help. Helping costs nothing but the day; it's good practice, and it's paid.
    /// </summary>
    public class HappeningWorker_Renown : HappeningWorker_Visitors
    {
        /// <summary>The names someone outside would seek out, the skill they'd need, and what they ask (a tag in the texts).</summary>
        private static readonly (string epithet, string skill, string tag)[] Asks =
        {
            ("Council_Epithet_Healer", "Medicine", "heal"), ("Council_Epithet_Surgeon", "Medicine", "heal"),
            ("Council_Epithet_Builder", "Construction", "build"), ("Council_Epithet_Delver", "Mining", "dig"),
            ("Council_Epithet_Farmer", "Plants", "grow"), ("Council_Epithet_Cook", "Cooking", "cook"),
            ("Council_Epithet_Tamer", "Animals", "beasts"), ("Council_Epithet_Hunter", "Animals", "hunt"),
            ("Council_Epithet_Scholar", "Intellectual", "study"), ("Council_Epithet_Maker", "Crafting", "make"),
            ("Council_Epithet_SilverTongue", "Social", "talk"), ("Council_Epithet_Defender", "Shooting", "defend"),
            ("Council_Epithet_Deadeye", "Shooting", "defend")
        };

        private static IEnumerable<(Pawn p, int i)> Known(Map map)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !S.epithets) yield break;
            foreach (var p in Colonists(map))
            {
                if (p.DevelopmentalStage.Juvenile() || p.Downed || p.InMentalState) continue;
                foreach (var e in comp.epithets.Of(p))
                {
                    int i = Array.FindIndex(Asks, x => x.epithet == e.defName);
                    if (i < 0) continue;
                    var sk = DefDatabase<SkillDef>.GetNamedSilentFail(Asks[i].skill);
                    if (sk != null && p.skills?.GetSkill(sk) is SkillRecord r && !r.TotallyDisabled) yield return (p, i);
                }
            }
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Known(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var pick = Known(ctx.map).RandomElementWithFallback();
            if (pick.p == null) return false;
            var epithet = DefDatabase<EpithetDef>.GetNamedSilentFail(Asks[pick.i].epithet);
            ctx.faction = Arrivals.Friendly();
            if (epithet == null || ctx.faction == null) return false;
            ctx.a = pick.p;
            ctx.variant = pick.i;
            ctx.skill = DefDatabase<SkillDef>.GetNamedSilentFail(Asks[pick.i].skill);
            ctx.tale = epithet.label;
            MakeGroup(ctx, ext, 1, ext.ages);
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.variant >= 0 && ctx.variant < Asks.Length) tags.Add(Asks[ctx.variant].tag);
        }
    }

    // ===================== A chance at vengeance =====================

    /// <summary>
    /// A small band of the people a colonist swore vengeance on is camped nearby. The colony can draw them into a fight
    /// on its own ground (a share of the storyteller's raid strength, with battles switched on), or let them pass.
    /// </summary>
    public class HappeningWorker_VendettaBand : IncidentWorker_Happening
    {
        private static Vendetta Usable(Map map)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !S.vendettas) return null;
            return comp.vendettas.vendettas.Where(v => v.Active && Raids.CanRaid(v.faction) && LivingEvents.AtHome(v.avenger, map) && !v.avenger.DevelopmentalStage.Juvenile()
                                                       && !v.avenger.Downed && !v.avenger.InMentalState && !v.avenger.WorkTagIsDisabled(WorkTags.Violent))
                .RandomElementWithFallback();
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => Usable(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var v = Usable(ctx.map);
            if (v == null) return false;
            ctx.a = v.avenger;
            ctx.enemy = v.faction;
            ctx.dead = v.victim;
            ctx.variant = v.kidnapped ? 1 : 0;
            return !ctx.dead.NullOrEmpty();
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(ctx.variant == 1 ? "taken" : "killed");
            if (ctx.enemy != null && ctx.enemy.def.techLevel <= TechLevel.Neolithic) tags.Add("tribalfoe");
        }
    }

    // ===================== Founding day =====================

    /// <summary>
    /// The colony's founding day, once a year: an old friend among the neighbors comes to mark it, and the colony can
    /// hold a feast or keep it quiet. The letter says who has been here since the first days, how many were lost, what
    /// the colony is known for and the customs it keeps. Fired on the day itself (Rimfolk's own round), not left to chance.
    /// </summary>
    public class HappeningWorker_Founding : HappeningWorker_Visitors
    {
        public const string DefName = "Council_Evt_Founding";

        private static int Years => GenDate.DaysPassed / GenDate.DaysPerYear;

        /// <summary>The first three days of a new year since the colony was founded.</summary>
        private static bool OnTheDay => GenDate.DaysPassed >= GenDate.DaysPerYear && GenDate.DaysPassed % GenDate.DaysPerYear <= 2;

        private static float Here(Pawn p) => p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f;

        /// <summary>Colonists who have been here since the colony's first days.</summary>
        private static List<Pawn> Founders(Map map) =>
            Colonists(map).Where(p => Here(p) >= Mathf.Max(0, GenDate.DaysPassed - 10) * (float)GenDate.TicksPerDay).ToList();

        /// <summary>An old friend of the colony: the neighbor it gets on with best.</summary>
        private static Faction OldFriend() =>
            VarietyUtil.GoodNeighbors().Where(f => f.PlayerGoodwill >= 10 && Arrivals.PersonKind(f) != null).OrderByDescending(f => f.PlayerGoodwill).FirstOrDefault();

        protected override bool CanHappen(Map map, HappeningExtension ext) => OnTheDay && Colonists(map).Any(p => !p.DevelopmentalStage.Juvenile());

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var pawns = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Baby()).ToList();
            ctx.a = pawns.Where(p => !p.DevelopmentalStage.Juvenile()).OrderByDescending(Here).FirstOrDefault();
            if (ctx.a == null) return false;
            ctx.count = Mathf.Max(1, Years);
            ctx.placeName = ctx.map.Biome?.label ?? "";
            // Raw food for a feast: enough for everyone, and some.
            ctx.variant = Mathf.Clamp(pawns.Count * 4, 10, 120);
            ctx.faction = OldFriend();
            if (ctx.faction != null) MakeGroup(ctx, ext, 1, ext.ages);
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            // The guest is welcome, not needed: if they can't get here, the day goes on without them.
            if (ctx.stranger != null && Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, GatheringSpot(ctx.map), ext.stayHours.RandomInRange) == null)
            {
                Arrivals.Discard(ctx.stranger);
                ctx.stranger = null;
            }
            // The old friend brings a gift for the day.
            if (ctx.stranger != null) Loot.Deliver(Loot.Make(Rand.Range(100f, 300f) * LivingEvents.WealthFactor(ctx.map)), ctx.map, VarietyUtil.Near(ctx.a, ctx.map), false);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(Years <= 1 ? "first" : "years");
            tags.Add(ctx.stranger != null ? "guest" : "noguest");
            if (Find.StoryWatcher?.statsRecord?.colonistsKilled > 0) tags.Add("lost");
        }

        protected override string Extra(HappeningContext ctx)
        {
            var lines = VarietyUtil.More(def)?.extra;
            if (lines.NullOrEmpty()) return null;
            var parts = new List<string>();
            var founders = Founders(ctx.map);
            if (founders.Count > 0) parts.Add(Part(lines, ctx, "founders", TextUtil.JoinAnd(founders.Select(p => p.LabelShort).ToList())));
            int lost = Find.StoryWatcher?.statsRecord?.colonistsKilled ?? 0;
            if (lost > 0) parts.Add(Part(lines, ctx, "lost", lost.ToString()));
            var comp = CouncilGameComponent.Instance;
            string known = comp?.character.Describe();
            if (!known.NullOrEmpty()) parts.Add(Part(lines, ctx, "known", known));
            var customs = comp?.dilemmas.Traditions.Select(d => d.traditionLabel).Where(l => !l.NullOrEmpty()).ToList();
            if (!customs.NullOrEmpty()) parts.Add(Part(lines, ctx, "customs", TextUtil.JoinAnd(customs)));
            return string.Join("\n", parts.Where(p => !p.NullOrEmpty()));
        }

        /// <summary>One of the lines under the letter: only one tagged for this part.</summary>
        private static string Part(List<string> lines, HappeningContext ctx, string tag, string item)
        {
            var tags = new HashSet<string> { tag };
            Flavor.AddEraTags(tags);
            string s = Flavor.PickTagged(lines.Where(l => Flavor.Strip(l, out var t) != null && t != null && t.Contains(tag)), tags);
            return s.NullOrEmpty() ? null : ctx.Format(s.Replace("{ITEM}", item));
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option == 0 && ctx.map != null && DilemmaEffect_Consume.RawFood(ctx.map) < ctx.variant
                ? "CC_Dilemma_NotEnough".Translate(ctx.variant, "CC_Dilemma_RawFood".Translate()).ToString()
                : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option == 0 && !byDefault && ctx.map != null) new DilemmaEffect_Consume { what = "rawfood", count = ctx.variant }.Apply(ctx);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>Every hour: on the colony's founding day (in the daytime, with nothing else going on), the event comes.</summary>
        public static void TryOnTheDay()
        {
            if (!OnTheDay || !S.worldEvents) return;
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(DefName);
            var map = HappeningTracker.HomeMap();
            if (def == null || map == null) return;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 9 || hour > 19 || HappeningTracker.Waiting || GenHostility.AnyHostileActiveThreatToPlayer(map, false, false)) return;
            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            if (def.Worker.CanFireNow(parms)) Find.Storyteller.TryFire(new FiringIncident(def, null, parms));
        }
    }

    // ===================== The far market =====================

    /// <summary>
    /// A neighbor's caravan is going to a far market and offers to take some of the colony's surplus along, and bring
    /// back what it's short of (medicine, cloth, steel, components). It comes back a week or so later, usually with a
    /// fair trade, sometimes with less.
    /// </summary>
    public class HappeningWorker_FarMarket : IncidentWorker_Happening
    {
        /// <summary>What a colony runs short of, and how much per person counts as enough.</summary>
        private static IEnumerable<(ThingDef def, float perHead)> Needs()
        {
            if (Avail.IsPreIndustrial)
            {
                yield return (ThingDefOf.MedicineHerbal, 4f);
                yield return (ThingDefOf.Cloth, 25f);
                yield return (ThingDefOf.Steel, 30f);
            }
            else
            {
                yield return (ThingDefOf.MedicineIndustrial, 3f);
                yield return (ThingDefOf.ComponentIndustrial, 2f);
                yield return (ThingDefOf.Steel, 40f);
                yield return (ThingDefOf.Cloth, 25f);
            }
        }

        private static ThingDef Wanted(Map map)
        {
            int people = Mathf.Max(1, Colonists(map).Count);
            return Needs().Where(n => n.def != null).Select(n => (n.def, ratio: map.resourceCounter.GetCount(n.def) / (n.perHead * people)))
                .Where(x => x.ratio < 0.5f).OrderBy(x => x.ratio).Select(x => x.def).FirstOrDefault();
        }

        /// <summary>Plain goods that keep on the road: never silver, medicine, drugs or anything that rots.</summary>
        private static bool Sellable(ThingDef d) =>
            d != null && d != ThingDefOf.Silver && d.category == ThingCategory.Item && TradeabilityUtility.PlayerCanSell(d.tradeability) && !d.IsDrug && !d.IsCorpse
            && !d.IsMedicine && d.stackLimit >= 10 && d.BaseMarketValue >= 0.5f && d.BaseMarketValue <= 40f && d.GetCompProperties<CompProperties_Rottable>() == null;

        private static List<KeyValuePair<ThingDef, int>> Surplus(Map map, ThingDef wanted) =>
            map.resourceCounter.AllCountedAmounts.Where(kv => kv.Key != wanted && kv.Value >= 20 && Sellable(kv.Key) && kv.Value * kv.Key.BaseMarketValue >= 250f).ToList();

        private static bool HasTown(Faction f) => Find.WorldObjects.Settlements.Any(s => s.Faction == f);

        /// <summary>A town some way off, of any people the colony isn't at war with.</summary>
        private static string MarketTown(Map map, Faction except)
        {
            var towns = Find.WorldObjects.Settlements.Where(s => s.HasName && s.Faction != null && s.Faction != except && LivingEvents.Friendly(s.Faction)).ToList();
            if (towns.Count == 0) towns = Find.WorldObjects.Settlements.Where(s => s.HasName && LivingEvents.Friendly(s.Faction)).ToList();
            var far = towns.Where(s => LivingEvents.Distance(map.Tile, s.Tile) >= 12f).ToList();
            return (far.Count > 0 ? far : towns).RandomElementWithFallback()?.Name;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var wanted = Wanted(map);
            return wanted != null && Surplus(map, wanted).Count > 0 && Arrivals.Friendly(HasTown) != null && Colonists(map).Any(p => !p.DevelopmentalStage.Juvenile());
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var wanted = Wanted(map);
            ctx.faction = Arrivals.Friendly(HasTown);
            if (wanted == null || ctx.faction == null) return false;
            var pick = Surplus(map, wanted).RandomElementByWeightWithFallback(kv => kv.Value * kv.Key.BaseMarketValue);
            if (pick.Key == null) return false;
            float target = Rand.Range(250f, 600f) * LivingEvents.WealthFactor(map);
            int n = VarietyUtil.Round(Mathf.Clamp(Mathf.RoundToInt(target / pick.Key.BaseMarketValue), 10, Mathf.RoundToInt(pick.Value * 0.6f)));
            if (n < 10) return false;
            // A better price far away than any trader pays here.
            float worth = n * pick.Key.BaseMarketValue * Rand.Range(1.1f, 1.35f);
            ctx.note = pick.Key.defName;
            ctx.variant = n;
            ctx.tale = VarietyUtil.Bundle(pick.Key, n);
            ctx.thing = wanted;
            ctx.count = Mathf.Max(1, Mathf.RoundToInt(worth / Mathf.Max(0.5f, wanted.BaseMarketValue)));
            ctx.placeName = MarketTown(map, ctx.faction) ?? ctx.faction.Name;
            ctx.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? Colonists(map).FirstOrDefault(p => !p.DevelopmentalStage.Juvenile());
            return ctx.a != null;
        }

        protected override string Extra(HappeningContext ctx) =>
            VarietyUtil.Line(VarietyUtil.More(def)?.extra, ctx, null, VarietyUtil.Bundle(ctx.thing, VarietyUtil.InStores(ctx.map, ctx.thing)));

        public override string Unavailable(HappeningContext ctx, int option)
        {
            var sent = LivingEvents.ThingNamed(ctx.note);
            return option == 0 && sent != null && VarietyUtil.InStores(ctx.map, sent) < ctx.variant
                ? "CC_Dilemma_NotEnough".Translate(ctx.variant, sent.label).ToString()
                : null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var sent = LivingEvents.ThingNamed(ctx.note);
            if (option == 0 && !byDefault && sent != null && ctx.thing != null && VarietyUtil.TakeFromStores(ctx.map, sent, ctx.variant))
            {
                var m = LivingEvents.Schedule(LivingEvents.Market, null, ctx.faction, sent.defName, ctx.thing.defName + "|" + ctx.placeName, ctx.a, Rand.Range(6f, 9f));
                if (m != null)
                {
                    m.amount = ctx.variant;
                    m.times = ctx.count;
                }
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>The caravan is back: what the colony's goods fetched (now and then less than hoped), or word that it never came.</summary>
        public static void Return(Memo m)
        {
            var def = LivingEvents.EventOf(LivingEvents.Market);
            var more = LivingEvents.More(def);
            var map = HappeningTracker.HomeMap();
            if (def == null || map == null) return;
            var ctx = LivingEvents.Context(m, def, map);
            if (!LivingEvents.Friendly(m.faction) || ctx.thing == null)
            {
                LivingEvents.Say(ctx, more?.cancelled);
                return;
            }
            bool poor = Rand.Chance(0.2f);
            if (poor) ctx.count = Mathf.Max(1, Mathf.RoundToInt(ctx.count * Rand.Range(0.6f, 0.8f)));
            var spot = DropCellFinder.TradeDropSpot(map);
            LivingEvents.Hand(ctx.thing, ctx.count, map, spot);
            LivingEvents.Tell(ctx, more?.doneTitles, more?.done, new[] { poor ? "poor" : "fair" }, poor ? LetterDefOf.NeutralEvent : LetterDefOf.PositiveEvent,
                new LookTargets(new TargetInfo(spot, map)), VarietyUtil.Bundle(ctx.thing, ctx.count));
        }
    }

    // ===================== The harvest fair =====================

    /// <summary>
    /// In the fall a neighbor holds a harvest fair in their nearest town. The colony can send a cart of its best crop:
    /// it sells well, and the colony's best grower may take a prize (the better they are, the likelier). The results
    /// come back a few days later.
    /// </summary>
    public class HappeningWorker_HarvestFair : IncidentWorker_Happening
    {
        private static List<KeyValuePair<ThingDef, int>> Crops(Map map) =>
            map.resourceCounter.AllCountedAmounts.Where(kv => kv.Value >= 40 && kv.Key.IsWithinCategory(ThingCategoryDefOf.PlantFoodRaw) && !kv.Key.IsDrug).ToList();

        private static bool HasTown(Faction f) => Find.WorldObjects.Settlements.Any(s => s.Faction == f && s.HasName);

        public static int Price(ThingDef def, int n) => Mathf.Max(20, Mathf.RoundToInt(n * def.BaseMarketValue * 1.25f / 5f) * 5);

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            Crops(map).Count > 0 && Arrivals.Friendly(HasTown) != null && VarietyUtil.Best(map, SkillDefOf.Plants) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var crop = Crops(map).OrderByDescending(kv => kv.Value * kv.Key.BaseMarketValue).FirstOrDefault();
            ctx.faction = Arrivals.Friendly(HasTown);
            ctx.a = VarietyUtil.Best(map, SkillDefOf.Plants);
            if (crop.Key == null || ctx.faction == null || ctx.a == null) return false;
            ctx.thing = crop.Key;
            ctx.variant = VarietyUtil.Round(Mathf.Clamp(Mathf.RoundToInt(crop.Value * 0.3f), 20, 150));
            ctx.tale = VarietyUtil.Bundle(crop.Key, ctx.variant);
            ctx.count = Price(crop.Key, ctx.variant);
            ctx.placeName = LivingEvents.TownOf(ctx.faction, map) ?? ctx.faction.Name;
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            var home = DefDatabase<DilemmaDef>.GetNamedSilentFail("Council_Dilemma_HarvestHome");
            if (home != null && (CouncilGameComponent.Instance?.dilemmas.IsTradition(home) ?? false)) tags.Add("harvesthome");
        }

        protected override string Extra(HappeningContext ctx) =>
            VarietyUtil.Line(VarietyUtil.More(def)?.extra, ctx, null, VarietyUtil.Bundle(ctx.thing, VarietyUtil.InStores(ctx.map, ctx.thing)));

        public override string Unavailable(HappeningContext ctx, int option) =>
            option == 0 && ctx.thing != null && VarietyUtil.InStores(ctx.map, ctx.thing) < ctx.variant
                ? "CC_Dilemma_NotEnough".Translate(ctx.variant, ctx.thing.label).ToString()
                : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option == 0 && !byDefault && ctx.thing != null && VarietyUtil.TakeFromStores(ctx.map, ctx.thing, ctx.variant))
            {
                var m = LivingEvents.Schedule(LivingEvents.Fair, null, ctx.faction, ctx.thing.defName, ctx.placeName, ctx.a, Rand.Range(3f, 4f));
                if (m != null) m.amount = ctx.variant;
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>The fair is over: the silver, and maybe a prize for the colony's grower.</summary>
        public static void Results(Memo m)
        {
            var def = LivingEvents.EventOf(LivingEvents.Fair);
            var more = LivingEvents.More(def);
            var map = HappeningTracker.HomeMap();
            if (def == null || map == null) return;
            var ctx = LivingEvents.Context(m, def, map);
            if (!LivingEvents.Friendly(m.faction) || ctx.thing == null)
            {
                LivingEvents.Say(ctx, more?.cancelled);
                return;
            }
            int silver = Price(ctx.thing, m.amount);
            var grower = m.other;
            int level = grower?.skills?.GetSkill(SkillDefOf.Plants) is SkillRecord r && !r.TotallyDisabled ? r.Level : 0;
            bool prize = grower != null && !grower.Dead && Rand.Chance(Mathf.Clamp(0.1f + level * 0.03f, 0.1f, 0.75f));
            if (prize)
            {
                silver += Mathf.RoundToInt(Rand.Range(40f, 120f) / 5f) * 5;
                using (Causes.Push(LivingEvents.Cause(def)))
                    CouncilGameComponent.GiveThought(grower, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_FairPrize"));
            }
            ctx.count = silver;
            var spot = DropCellFinder.TradeDropSpot(map);
            VarietyUtil.PaySilver(map, spot, silver);
            LivingEvents.Tell(ctx, more?.doneTitles, more?.done, new[] { prize ? "prize" : "noprize" }, LetterDefOf.PositiveEvent, new LookTargets(new TargetInfo(spot, map)));
        }
    }

    // ===================== A guest on the longest night =====================

    /// <summary>
    /// A winter evening: a lone traveler asks to share the colony's fire for the night. Taken in, they talk with a
    /// colonist until late (about the colonist's own old life, if they have one in common) and are gone in the morning,
    /// leaving something behind. Or the colony sends them on with food, or keeps the gate shut.
    /// </summary>
    public class HappeningWorker_MidwinterGuest : HappeningWorker_Visitors
    {
        private static ThingDef BestMeal(Map map) =>
            map.resourceCounter.AllCountedAmounts.Where(kv => kv.Value > 0 && kv.Key.ingestible != null && kv.Key.ingestible.IsMeal && kv.Key.ingestible.HumanEdible && !kv.Key.IsDrug)
                .OrderByDescending(kv => (int)kv.Key.ingestible.preferability).Select(kv => kv.Key).FirstOrDefault();

        /// <summary>Hours until seven in the morning.</summary>
        private static int HoursToMorning(Map map)
        {
            int hour = GenLocalDate.HourOfDay(map);
            return hour < 7 ? 7 - hour : 31 - hour;
        }

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            int hour = GenLocalDate.HourOfDay(map);
            return hour >= 15 && hour <= 22 && base.CanHappen(map, ext);
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed)
                .RandomElementByWeightWithFallback(p => (p.story?.traits?.HasTrait(TraitDefOf.Kind) == true ? 2f : 1f) + (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0) * 0.1f);
            ctx.thing = BestMeal(ctx.map);
            ctx.tale = GenText.ToStringTemperature(ctx.map.mapTemperature.OutdoorTemp, "F0");
            ctx.placeName = ctx.map.Biome?.label ?? "";
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, GatheringSpot(ctx.map), HoursToMorning(ctx.map) + 4f) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(ctx.thing != null ? "meal" : "nomeal");
            if (ctx.map != null && ctx.map.mapTemperature.OutdoorTemp < -15f) tags.Add("bitter");
            var supper = DefDatabase<DilemmaDef>.GetNamedSilentFail("Council_Dilemma_Midwinter");
            if (supper != null && (CouncilGameComponent.Instance?.dilemmas.IsTradition(supper) ?? false)) tags.Add("supper");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var x = ctx.stranger;
            if (option == 0 && x != null && x.Spawned && ctx.map != null)
            {
                LivingEvents.Schedule(LivingEvents.Morning, x, ctx.faction, null, null, ctx.a, HoursToMorning(ctx.map) / 24f);
                // Two people with the same sort of past find each other by the fire.
                var pasts = ctx.a != null ? Pasts.Of(ctx.a).Select(Pasts.Tag).ToList() : new List<string>();
                if (pasts.Count > 0)
                {
                    string shared = pasts.RandomElement();
                    outcome = Flavor.PickTagged(VarietyUtil.More(def)?.outcomes, Tags(ctx, shared)) is string line && !line.NullOrEmpty() ? line : outcome;
                }
            }
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        private static HashSet<string> Tags(HappeningContext ctx, string past)
        {
            var tags = new HashSet<string> { past };
            Flavor.AddEraTags(tags);
            return tags;
        }

        /// <summary>Morning: the guest has gone on, and left something by the fire.</summary>
        public static void Morning(Memo m)
        {
            var def = LivingEvents.EventOf(LivingEvents.Morning);
            var more = LivingEvents.More(def);
            var map = HappeningTracker.HomeMap();
            if (def == null || map == null) return;
            var ctx = LivingEvents.Context(m, def, map);
            var x = m.pawn;
            // Something befell them in the night (or they joined the colony after all): no parting gift to tell of.
            if (x == null || x.Dead || x.Faction == Faction.OfPlayer) return;
            var near = x.Spawned && x.Map == map ? x.Position : GatheringSpot(map) ?? DropCellFinder.TradeDropSpot(map);
            var things = Loot.Make(Rand.Range(80f, 200f) * LivingEvents.WealthFactor(map));
            if (things.Count == 0) return;
            Loot.Deliver(things, map, near, false);
            ctx.tale = Loot.Describe(things);
            if (x.Spawned) Arrivals.SendAway(new[] { x });
            var first = things.FirstOrDefault(t => t.Spawned);
            LivingEvents.Say(ctx, more?.done, null, null, MessageTypeDefOf.PositiveEvent, first != null ? new LookTargets(first) : null);
        }
    }

    // ===================== The thaw =====================

    /// <summary>
    /// Spring, where the winters are hard: the first truly warm day. Everyone's glad of it, and the melt turns up
    /// something somebody lost on the road over the winter.
    /// </summary>
    public class HappeningWorker_Thaw : IncidentWorker_Happening
    {
        private static IEnumerable<Pawn> Outside(Map map) =>
            Colonists(map).Where(p => !p.DevelopmentalStage.Baby() && p.Awake() && !p.Drafted && !p.Downed && !p.Position.Roofed(map));

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            GenTemperature.MinTemperatureAtTile(map.Tile) < -3f && Colonists(map).Any(p => !p.DevelopmentalStage.Baby());

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.a = Outside(ctx.map).RandomElementWithFallback() ?? Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Baby()).RandomElementWithFallback();
            ctx.placeName = ctx.map.Biome?.label ?? "";
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var things = Loot.Make(Rand.Range(60f, 160f) * LivingEvents.WealthFactor(ctx.map));
            if (things.Count == 0) return false;
            Loot.Deliver(things, ctx.map, VarietyUtil.Near(ctx.a, ctx.map), false);
            ctx.place = things.FirstOrDefault(t => t.Spawned);
            ctx.tale = Loot.Describe(things);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            tags.Add(GenDate.DaysPassed < GenDate.DaysPerYear ? "first" : "again");
            var year = DefDatabase<DilemmaDef>.GetNamedSilentFail("Council_Dilemma_NameTheYear");
            if (year != null && (CouncilGameComponent.Instance?.dilemmas.IsTradition(year) ?? false)) tags.Add("nameyear");
            if (ctx.a?.DevelopmentalStage.Child() == true) tags.Add("child");
        }
    }

    // ===================== Shards from the sky (A RimWorld of Magic) =====================

    /// <summary>
    /// A RimWorld of Magic: in the night, pale lights come down near the colony and leave unrefined magicyte behind. A
    /// mage of the colony felt it coming.
    /// </summary>
    public class HappeningWorker_Starfall : IncidentWorker_Happening
    {
        private static ThingDef Shard => DefDatabase<ThingDef>.GetNamedSilentFail("RawMagicyte");

        /// <summary>A mage of any class (A RimWorld of Magic marks them with a hediff).</summary>
        private static bool IsMage(Pawn p) => p.health?.hediffSet?.hediffs?.Any(h => h.def.defName == "TM_MagicUserHD") == true;

        protected override bool CanHappen(Map map, HappeningExtension ext) => Shard != null && Colonists(map).Any(p => !p.DevelopmentalStage.Baby());

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var pawns = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Baby()).ToList();
            var mage = pawns.Where(IsMage).RandomElementWithFallback();
            ctx.a = mage ?? pawns.Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback() ?? pawns.RandomElementWithFallback();
            ctx.variant = mage != null ? 1 : 0;
            ctx.thing = Shard;
            return ctx.a != null && ctx.thing != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var center = VarietyUtil.Near(ctx.a, map);
            if (!center.IsValid) center = map.Center;
            int total = 0;
            for (int i = 0, falls = Rand.RangeInclusive(2, 3); i < falls; i++)
            {
                if (!CellFinderLoose.TryGetRandomCellWith(c => c.Standable(map) && !c.Fogged(map) && !c.Roofed(map) && c.GetFirstBuilding(map) == null
                                                               && c.DistanceTo(center) >= 10f && c.DistanceTo(center) <= 45f, map, 400, out var cell)) continue;
                int n = Rand.RangeInclusive(12, 30);
                LivingEvents.Hand(ctx.thing, n, map, cell);
                if (!ctx.spot.IsValid) ctx.spot = cell;
                total += n;
            }
            ctx.count = total;
            if (total == 0) return false;
            if (ctx.variant == 1)
            {
                using (Causes.Push(LivingEvents.Cause(def)))
                    CouncilGameComponent.GiveThought(ctx.a, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_FeltStarfall"));
            }
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(ctx.variant == 1 ? "mage" : "nomage");
    }

    // ===================== A strange creature (Alpha Animals) =====================

    /// <summary>
    /// Alpha Animals: one of its rarer, gentler creatures wanders onto the colony's land. The colony's best handler can
    /// try to win it over (the better they are and the tamer it is, the likelier), or the colony lets it be and it moves
    /// on. Never a predator, and nobody gets hurt either way.
    /// </summary>
    public class HappeningWorker_RareCreature : IncidentWorker_Happening
    {
        public const string Mod = "sarg.alphaanimals";

        private static float Wild(PawnKindDef k) => k.race.GetStatValueAbstract(StatDefOf.Wildness);

        private static HashSet<PawnKindDef> wildSomewhere;

        /// <summary>Animals that live wild in some land (not summoned, not a hive's, not one an event brings).</summary>
        private static HashSet<PawnKindDef> WildSomewhere => wildSomewhere ??= new HashSet<PawnKindDef>(
            DefDatabase<BiomeDef>.AllDefsListForReading.SelectMany(b => b.AllWildAnimals ?? Enumerable.Empty<PawnKindDef>()));

        private static IEnumerable<PawnKindDef> Kinds(Map map) =>
            DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => k.race?.race != null && k.RaceProps.Animal && LivingEvents.FromMod(k.race, Mod) && WildSomewhere.Contains(k) && !k.RaceProps.Insect
                                                                      && !k.RaceProps.Dryad && !k.RaceProps.predator && !k.RaceProps.IsMechanoid
                                                                      && k.RaceProps.baseBodySize >= 0.3f && k.RaceProps.baseBodySize <= 4f && k.combatPower <= 220f
                                                                      && Wild(k) >= 0.35f && Wild(k) <= 0.97f
                                                                      && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f));

        private static IEnumerable<Pawn> Handlers(Map map) =>
            Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed && !p.InMentalState && !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling));

        protected override bool CanHappen(Map map, HappeningExtension ext) => Handlers(map).Any() && Kinds(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            var native = map.Biome?.AllWildAnimals?.ToList() ?? new List<PawnKindDef>();
            // Those that live in this land are much likelier.
            var kind = Kinds(map).RandomElementByWeightWithFallback(k => native.Contains(k) ? 3f : 1f);
            ctx.a = Handlers(map).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();
            if (kind == null || ctx.a == null) return false;
            var animal = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
            if (animal == null) return false;
            ctx.stranger = animal;
            ctx.thing = kind.race;
            ctx.beast = Find.ActiveLanguageWorker.WithIndefiniteArticle(kind.label ?? kind.race.label);
            ctx.variant = native.Contains(kind) ? 1 : 0;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var map = ctx.map;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Animal)) return false;
            GenSpawn.Spawn(ctx.stranger, cell, map);
            // It won't stay long.
            ctx.stranger.mindState.exitMapAfterTick = Find.TickManager.TicksGame + Mathf.RoundToInt(GenDate.TicksPerDay * 1.5f);
            return true;
        }

        protected override void Abort(HappeningContext ctx)
        {
            var x = ctx.stranger;
            if (x != null && !x.Spawned && !x.Discarded) Find.WorldPawns.PassToWorld(x, PawnDiscardDecideMode.Discard);
            ctx.stranger = null;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(ctx.variant == 1 ? "native" : "stranger");

        /// <summary>The risk is that it bolts: likelier the wilder it is, less likely the better the handler.</summary>
        public override float RiskChance(HappeningContext ctx, int option, float chance)
        {
            if (option != 0 || ctx.stranger == null) return base.RiskChance(ctx, option, chance);
            int level = ctx.a?.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0;
            float wild = ctx.stranger.GetStatValue(StatDefOf.Wildness);
            return Mathf.Clamp(0.8f - level * 0.04f + (wild - 0.5f) * 0.6f, 0.1f, 0.9f);
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var x = ctx.stranger;
            // Startled, it's off within the hour. Won over, it stays.
            if (option == 0 && risk && x != null && x.Spawned && x.Faction == null) x.mindState.exitMapAfterTick = Find.TickManager.TicksGame + GenDate.TicksPerHour;
            if (x != null && x.Faction == Faction.OfPlayer && x.mindState != null) x.mindState.exitMapAfterTick = -99999;
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== A bath for the road (Dubs Bad Hygiene) =====================

    /// <summary>
    /// Dubs Bad Hygiene: travelers who have been on the road for weeks have heard about the colony's sauna (or hot tub,
    /// bathtub, pool) and ask to use it. For silver, for nothing, or not today.
    /// </summary>
    public class HappeningWorker_Bathhouse : HappeningWorker_Visitors
    {
        public const string Mod = "dubwise.dubsbadhygiene";

        private static readonly string[] Kinds = { "sauna", "hottub", "pool", "tub" };

        /// <summary>What sort of bath a building is, by its name (the sauna's heater, not its benches), or -1.</summary>
        private static int KindOf(ThingDef d)
        {
            string n = d?.defName ?? "";
            if (n.Contains("Sauna") && !n.Contains("Seat")) return 0;
            if (n.Contains("HotTub")) return 1;
            if (n.Contains("SwimmingPool")) return 2;
            if (n.Contains("Bathtub")) return 3;
            return -1;
        }

        private static Building Bath(Map map) =>
            map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && KindOf(b.def) >= 0).OrderBy(b => KindOf(b.def)).FirstOrDefault();

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Bath(map) != null;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var bath = Bath(ctx.map);
            ctx.faction = Arrivals.Friendly();
            if (bath == null || ctx.faction == null) return false;
            ctx.place = bath;
            ctx.variant = KindOf(bath.def);
            int n = Mathf.Max(2, ext.count.RandomInRange);
            MakeGroup(ctx, ext, n, ext.ages);
            ctx.count = Mathf.RoundToInt(n * (Avail.IsPreIndustrial ? 12f : 20f) / 5f) * 5;
            ctx.a = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            return ctx.stranger != null && ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(ctx.group.Prepend(ctx.stranger).ToList(), ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            if (ctx.variant >= 0 && ctx.variant < Kinds.Length) tags.Add(Kinds[ctx.variant]);
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (option == 0 && !byDefault && ctx.map != null)
                VarietyUtil.PaySilver(ctx.map, ctx.place != null && ctx.place.Spawned ? ctx.place.Position : IntVec3.Invalid, ctx.count);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    // ===================== A guest of note (Hospitality) =====================

    /// <summary>
    /// Hospitality: someone of standing among a neighboring people writes ahead to ask for the colony's guest room in a
    /// few days. When they come, they judge the room they're given (its impressiveness): a fine room pleases them and
    /// their people, a poor one doesn't, so the colony has a few days to make it ready.
    /// </summary>
    public class HappeningWorker_NotableGuest : HappeningWorker_Visitors
    {
        public const string Mod = "orion.hospitality";

        /// <summary>Beds Hospitality has made guest beds.</summary>
        private static IEnumerable<Building_Bed> GuestBeds(Map map) =>
            map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().Where(b => b.Spawned && b.GetType().Name == "Building_GuestBed");

        /// <summary>How impressive the best guest room is (-1 with no guest bed indoors), and its bed.</summary>
        private static float BestRoom(Map map, out Building_Bed best)
        {
            float score = -1f;
            best = null;
            foreach (var bed in GuestBeds(map))
            {
                var room = bed.GetRoom();
                if (room == null || room.PsychologicallyOutdoors) continue;
                float s = room.GetStat(RoomStatDefOf.Impressiveness);
                if (s <= score) continue;
                score = s;
                best = bed;
            }
            return score;
        }

        private static float BestRoom(Map map) => BestRoom(map, out _);

        private static string Stage(float score) => score < 0f ? "" : RoomStatDefOf.Impressiveness.GetScoreStage(score)?.label ?? "";

        private static bool Expected => CouncilGameComponent.Instance?.happenings.Memos(LivingEvents.Guest).Any() ?? false;

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            !Expected && GuestBeds(map).Any() && Arrivals.Friendly(VarietyUtil.OnGoodTerms) != null && Colonists(map).Any(p => !p.DevelopmentalStage.Juvenile());

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly(VarietyUtil.OnGoodTerms);
            if (ctx.faction == null) return false;
            MakeGroup(ctx, ext, 1, ext.ages);
            if (ctx.stranger == null) return false;
            Arrivals.Hold(ctx.stranger);
            ctx.xWaiting = true;
            ctx.a = VarietyUtil.Best(ctx.map, SkillDefOf.Social) ?? Colonists(ctx.map).FirstOrDefault(p => !p.DevelopmentalStage.Juvenile());
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override string Extra(HappeningContext ctx)
        {
            float score = BestRoom(ctx.map);
            return VarietyUtil.Line(VarietyUtil.More(def)?.extra, ctx, new[] { score < 0f ? "noroom" : "room" }, Stage(score));
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            var x = ctx.stranger;
            if (option == 0 && x != null && !x.Dead && !x.Spawned && LivingEvents.Friendly(ctx.faction))
                LivingEvents.Schedule(LivingEvents.Guest, x, ctx.faction, null, ctx.placeName, ctx.a, Rand.Range(2f, 3f));
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }

        /// <summary>Expected: the memo holds them now. Otherwise they stay home.</summary>
        public override void Cleanup(HappeningContext ctx)
        {
            var x = ctx.stranger;
            if (x != null && (CouncilGameComponent.Instance?.happenings.Holds(x) ?? false)) return;
            base.Cleanup(ctx);
        }

        /// <summary>The guest arrives and is shown the best guest room.</summary>
        public static void Arrive(Memo m)
        {
            var def = LivingEvents.EventOf(LivingEvents.Guest);
            var more = LivingEvents.More(def);
            var map = HappeningTracker.HomeMap();
            if (def == null || map == null) return;
            var ctx = LivingEvents.Context(m, def, map);
            var x = m.pawn;
            if (!LivingEvents.FreeToTravel(x) || x.Faction != m.faction
                || Arrivals.Visit(new List<Pawn> { x }, map, m.faction, GatheringSpot(map), Rand.Range(18f, 26f)) == null)
            {
                LivingEvents.Say(ctx, more?.cancelled);
                return;
            }
            float score = BestRoom(map, out var bed);
            string tier = score < 0f ? "noroom" : score >= 65f ? "great" : score >= 35f ? "good" : "poor";
            var f = m.faction;
            int goodwill = tier == "great" ? 10 : tier == "good" ? 5 : 1;
            if (f.HasGoodwill) f.TryAffectGoodwillWith(Faction.OfPlayer, goodwill, true, false);
            if (tier == "great")
            {
                // Left in the guest room, with thanks.
                Loot.Deliver(Loot.Make(Rand.Range(200f, 450f) * LivingEvents.WealthFactor(map), true), map, bed?.Position ?? x.Position, false);
                var host = m.other;
                if (host != null && !host.Dead && host.IsColonist)
                {
                    using (Causes.Push(LivingEvents.Cause(def)))
                        CouncilGameComponent.GiveThought(host, DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Evt_GuestPraised"));
                }
            }
            LivingEvents.Tell(ctx, more?.doneTitles, more?.done, new[] { tier }, tier == "great" || tier == "good" ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent,
                new LookTargets(x), Stage(score));
        }
    }

    // ===================== A kindred spirit =====================

    /// <summary>
    /// A traveler who once set out to do the very thing a colonist has set their heart on (their life goal) stops for
    /// the night, and the two talk until late. The colonist comes away encouraged, and with some practice.
    /// </summary>
    public class HappeningWorker_KindredSpirit : HappeningWorker_Visitors
    {
        /// <summary>Goals that only make sense here (about this colony, its council, someone's own nemesis) aren't shared with a stranger.</summary>
        private static readonly string[] Unshared = { "colony", "council", "nemesis", "place" };

        private static bool Shareable(Aspiration a) => a?.def != null && !a.done && !a.def.goal.NullOrEmpty() && !Unshared.Any(w => a.def.goal.Contains(w));

        private static IEnumerable<Pawn> Dreamers(Map map)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !S.aspirations) return Enumerable.Empty<Pawn>();
            return Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile() && !p.Downed && Shareable(comp.aspirations.Active(p)));
        }

        protected override bool CanHappen(Map map, HappeningExtension ext) => base.CanHappen(map, ext) && Dreamers(map).Any();

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            ctx.a = Dreamers(ctx.map).RandomElementWithFallback();
            if (ctx.faction == null || ctx.a == null) return false;
            ctx.skill = CouncilGameComponent.Instance.aspirations.Active(ctx.a)?.skill;
            MakeGroup(ctx, ext, 1, ext.ages);
            return ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, GatheringSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add(ctx.skill != null ? "skill" : "noskill");
    }
}
