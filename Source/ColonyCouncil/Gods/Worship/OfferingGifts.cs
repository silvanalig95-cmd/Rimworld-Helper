using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Precious things, and gifts asked of the colony first (the player, 16 November 2026: "it should please never take very
    /// valuable items like components etc. as offering, or only if there is a proper payoff for it. but then it shouldn't take
    /// it automatically, but perhaps an event where a pawn asks for approval first").
    /// <list type="bullet">
    /// <item><see cref="Precious"/>: what is precious to the colony is never carried to a god unasked (Offerings.Pick): the
    /// things the player named (components, advanced components, plasteel, uranium, gold, jade, neutroamine), every mod's
    /// parts for machines (a manufactured piece worth 25 or more), medicine past herbal, glitterworld goods (traded as exotic,
    /// or of the spacer age), a material worth more than 5 a piece, and anything the colony can't make now or gather again by
    /// its own recipes, benches and land (OfferingGiftMath.Precious). What it has plenty of and can make or gather again is
    /// what its people carry on their own: food, wood, hides, wool, stone blocks, herbs, simple crafts and the like.</item>
    /// <item><see cref="Daily"/>: now and then someone devout comes to the player (a card with Yes and No, through the gods'
    /// questions, GodAsks) wanting to give something precious of the colony's to their god, a thing it loves, saying why in
    /// plain words and what they hope it brings, as they believe it; or the god asked for a kind of thing in a dream and only
    /// something precious of that kind is left. On Yes they carry it (Offerings.Send), for a stronger favour and a likelier,
    /// larger answer (OfferingGiftMath.Lean, ReturnChance); on No they let it be, a little disappointed if devout, and the
    /// same ask doesn't come back for a while (OfferingGiftMath.AskNow and its waits).</item>
    /// </list>
    /// </summary>
    public static class OfferingGifts
    {
        // ================= Precious =================

        /// <summary>The things the player named as never to be taken unasked (the game's own; a mod's like of them goes by what it is).</summary>
        private static readonly HashSet<string> Named = new HashSet<string> { "ComponentIndustrial", "ComponentSpacer", "Plasteel", "Uranium", "Gold", "Jade", "Neutroamine" };

        /// <summary>A mod's drink or smoke worth no more than this a piece is made again where a mod's barrel or still stands.</summary>
        private const float CheapDrink = 20f;

        private static Map cacheMap;
        private static int cacheDay = -1;
        private static readonly Dictionary<ThingDef, bool> cache = new Dictionary<ThingDef, bool>();
        private static HashSet<ThingDef> made = new HashSet<ThingDef>(), gathered = new HashSet<ThingDef>();
        private static bool processor;
        private static List<ThingDef> mineables;

        /// <summary>Precious to the colony at this home: never carried to a god unless someone asks and the colony says yes. Worked out once a day a home.</summary>
        public static bool Precious(ThingDef d, Map map)
        {
            if (d == null || map == null) return true;
            int day = Find.TickManager.TicksGame / GenDate.TicksPerDay;
            if (cacheMap != map || cacheDay != day) Rebuild(map, day);
            if (cache.TryGetValue(d, out bool p)) return p;
            try
            {
                p = OfferingGiftMath.Precious(FactsOf(d));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: whether a thing is precious", e);
                p = true;
            }
            cache[d] = p;
            return p;
        }

        internal static PreciousFacts FactsOf(ThingDef d) => new PreciousFacts
        {
            named = Named.Contains(d.defName),
            part = d.IsWithinCategory(ThingCategoryDefOf.Manufactured) && d.BaseMarketValue >= OfferingGiftMath.PartValue && !d.IsIngestible && !d.IsMedicine,
            medicine = d.IsMedicine,
            potency = d.IsMedicine ? d.GetStatValueAbstract(StatDefOf.MedicalPotency) : 0f,
            exotic = d.tradeTags != null && d.tradeTags.Contains("ExoticMisc") || d.techLevel >= TechLevel.Spacer,
            stuff = d.IsStuff,
            unitValue = d.BaseMarketValue,
            renewable = Renewable(d)
        };

        /// <summary>
        /// The colony can make it now or gather it again: a recipe of a bench it has built with the research done; a plant on
        /// its land or in its fields, a rock it can mine, an animal on its land (meat, hide) or of its own (milk, wool, eggs);
        /// food of any kind (grown, hunted, cooked), herbs, wood, hides, wool, cloth and the plainer fabrics (cotton is sown
        /// anywhere), stone blocks, the colony's coin; a vat's beer where a barrel stands, and a mod's cheap drink or smoke
        /// where a mod's barrel or still stands.
        /// </summary>
        private static bool Renewable(ThingDef d)
        {
            if (made.Contains(d) || gathered.Contains(d)) return true;
            if (d.IsMedicine) return d.GetStatValueAbstract(StatDefOf.MedicalPotency) <= OfferingGiftMath.HerbalPotency + 0.05f;
            if (d.IsIngestible && !d.IsDrug && d.IsNutritionGivingIngestible) return true;
            if (d.IsLeather || d.IsWithinCategory(ThingCategoryDefOf.Wools)) return true;
            if (d.IsWithinCategory(ThingCategoryDefOf.Textiles) && d.BaseMarketValue <= OfferingGiftMath.PreciousStuffValue) return true;
            if (d == ThingDefOf.WoodLog || d.IsStuff && d.stuffProps.categories != null && d.stuffProps.categories.Contains(StuffCategoryDefOf.Woody) && d.IsWithinCategory(ThingCategoryDefOf.ResourcesRaw))
                return true;
            if (d.IsWithinCategory(ThingCategoryDefOf.StoneBlocks)) return true;
            if (d == ThingDefOf.Silver) return true;
            if (d.IsDrug && processor && d.BaseMarketValue <= CheapDrink) return true;
            return false;
        }

        /// <summary>What the colony at this home can make now and gather again, read once a day.</summary>
        private static void Rebuild(Map map, int day)
        {
            cacheMap = map;
            cacheDay = day;
            cache.Clear();
            made = new HashSet<ThingDef>();
            gathered = new HashSet<ThingDef>();
            processor = false;
            try
            {
                foreach (var bd in map.listerBuildings.allBuildingsColonist.Where(x => x?.def != null).Select(x => x.def).Distinct().ToList())
                {
                    foreach (var r in bd.AllRecipes ?? new List<RecipeDef>())
                    {
                        if (r?.products == null || !r.AvailableNow) continue;
                        foreach (var pr in r.products)
                            if (pr?.thingDef != null)
                                made.Add(pr.thingDef);
                    }
                    if (bd == ThingDefOf.FermentingBarrel) made.Add(ThingDefOf.Beer);
                    if (bd.thingClass != null && bd.thingClass.Name.Contains("Processor") || bd.comps != null && bd.comps.Any(c => c != null && c.GetType().Name.Contains("Processor")))
                        processor = true;
                }
                var plants = new HashSet<ThingDef>();
                foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))
                    if (t?.def != null && plants.Add(t.def) && t.def.plant?.harvestedThingDef != null)
                        gathered.Add(t.def.plant.harvestedThingDef);
                foreach (var z in map.zoneManager.AllZones.OfType<Zone_Growing>())
                {
                    var h = z.GetPlantDefToGrow()?.plant?.harvestedThingDef;
                    if (h != null) gathered.Add(h);
                }
                mineables ??= DefDatabase<ThingDef>.AllDefsListForReading.Where(x => x?.building?.mineableThing != null).ToList();
                foreach (var rock in mineables)
                    if (map.listerThings.ThingsOfDef(rock).Count > 0)
                        gathered.Add(rock.building.mineableThing);
                foreach (var a in map.mapPawns.AllPawnsSpawned)
                {
                    if (a?.RaceProps == null || !a.RaceProps.Animal || a.Dead || a.Faction != null && a.Faction != Faction.OfPlayer) continue;
                    if (a.RaceProps.leatherDef != null) gathered.Add(a.RaceProps.leatherDef);
                    if (a.RaceProps.meatDef != null) gathered.Add(a.RaceProps.meatDef);
                    if (a.Faction != Faction.OfPlayer) continue;
                    var milk = a.def.GetCompProperties<CompProperties_Milkable>()?.milkDef;
                    if (milk != null) gathered.Add(milk);
                    var wool = a.def.GetCompProperties<CompProperties_Shearable>()?.woolDef;
                    if (wool != null) gathered.Add(wool);
                    var eggs = a.def.GetCompProperties<CompProperties_EggLayer>();
                    if (eggs?.eggUnfertilizedDef != null) gathered.Add(eggs.eggUnfertilizedDef);
                    if (eggs?.eggFertilizedDef != null) gathered.Add(eggs.eggFertilizedDef);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: what the colony can make and gather", e);
            }
        }

        /// <summary>How much of it the colony holds at this home.</summary>
        internal static int Held(Map map, ThingDef d)
        {
            int have = 0;
            var list = map.listerThings.ThingsOfDef(d);
            for (int i = 0; i < list.Count; i++) have += list[i].stackCount;
            return have;
        }

        /// <summary>The store of it the colony keeps whatever it gives: medicine for everyone, two meals each, a purse of silver, a few parts.</summary>
        internal static int Keep(ThingDef d, int colonists)
        {
            var k = Offerings.Kind(d);
            if (k == OfferingKind.Medicine) return 4 * colonists;
            if (k == OfferingKind.Meal) return 2 * colonists;
            if (k == OfferingKind.Silver) return 300;
            if (k == OfferingKind.Component) return 12;
            return 0;
        }

        // ================= Asked first =================

        private const string AskMark = "gift-ask", AskGodMark = "gift-ask-god", NoMark = "gift-no|", DreamMark = "gift-dream";

        /// <summary>A home's mark of the asks (WorshipBook.Prune lets one go once its wait is long past).</summary>
        internal static bool Spent(HomeMark m, int now)
        {
            if (m?.what == null || !m.what.StartsWith("gift-", StringComparison.Ordinal)) return false;
            if (m.what.StartsWith(NoMark, StringComparison.Ordinal) || m.what == DreamMark) return m.until < now;
            return now - m.value > GenDate.TicksPerDay * 90;
        }

        private static HomeMark MarkOf(WorshipBook b, int home, string what, int god) =>
            b.homeMarks.LastOrDefault(x => x != null && x.home == home && x.what == what && (god < 0 || x.god == god));

        private static float Since(WorshipBook b, int home, string what, int god, int now)
        {
            var m = MarkOf(b, home, what, god);
            return m == null ? 9999f : (now - m.value) / (float)GenDate.TicksPerDay;
        }

        private static void Mark(WorshipBook b, int home, string what, int god, int value, int until)
        {
            var m = b.homeMarks.FirstOrDefault(x => x != null && x.home == home && x.what == what && x.god == god);
            if (m == null) b.homeMarks.Add(m = new HomeMark { home = home, what = what, god = god });
            m.value = value;
            m.until = until;
            while (b.homeMarks.Count > WorshipBook.MaxHomeMarks) b.homeMarks.RemoveAt(0);
        }

        /// <summary>An ask of this home's still waits for the colony's answer, or a gift it said yes to waits to be carried.</summary>
        private static bool Waiting(WorshipBook b, Map map)
        {
            int key = Homes.KeyOf(map);
            if (b.offerings.Any(o => o != null && o.precious && o.fate == OfferingFate.Waiting && o.home == Gods.HomeKey(map))) return true;
            return Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_GodWorld>().Any(l => l.ask == GodAsks.Precious && l.home == key && l.Waiting);
        }

        /// <summary>The region's day at a home: now and then someone devout asks to give something precious of the colony's to their god.</summary>
        public static void Daily(WorshipBook b, Map map, int now)
        {
            if (b == null || map == null || !Tell.CanAsk || Waiting(b, map)) return;
            if (Since(b, Gods.HomeKey(map), AskMark, -1, now) < OfferingGiftMath.AskGapDays) return;
            Ask(b, map, now, null, null, null, false);
        }

        /// <summary>
        /// The god asked in a dream for a kind of thing, and only something precious of that kind is left (Offerings.Send): the
        /// dreamer asks the colony for it, once a dream.
        /// </summary>
        internal static void AskForDemand(WorshipBook b, Map map, God g, DemandRecord d)
        {
            if (b == null || map == null || g == null || d?.dreamer == null || d.done || d.met) return;
            int home = Gods.HomeKey(map), now = Worship.Now;
            var m = MarkOf(b, home, DreamMark, g.id);
            if (m != null && m.until == d.until || Waiting(b, map)) return;
            Mark(b, home, DreamMark, g.id, now, d.until);
            Ask(b, map, now, d.dreamer, g, d, true);
        }

        /// <summary>Someone who may ask: one of the colony's own, grown, devout, never a scoffer, on this map and not in a fight or down.</summary>
        private static bool CanAsk(Pawn p, Map map) =>
            p != null && p.Spawned && p.Map == map && Worship.Ours(p) && !p.DevelopmentalStage.Juvenile() && !Worship.Scoffer(p) && !p.Downed && !p.Drafted
            && !p.InMentalState;

        /// <summary>The place a precious gift goes to: the god's temple first, else any of its places here, none waiting to be dedicated.</summary>
        internal static Thing ShrineFor(Map map, God g) =>
            map == null || g == null ? null : Shrines.Of(map, g).Where(s => s != null && !s.pending && s.Live).Select(s => s.thing).OrderByDescending(Shrines.IsTemple).FirstOrDefault();

        /// <summary>
        /// Someone asks (the priest first, then the devout, or only <paramref name="only"/>, for <paramref name="onlyGod"/>): the
        /// first who holds a god with a place here that takes gifts, and something precious of its liking (or of the dream's
        /// kind) the colony can spare and they can reach. <paramref name="anyway"/>: the day's chance and the waits give way.
        /// </summary>
        internal static bool Ask(WorshipBook b, Map map, int now, Pawn only, God onlyGod, DemandRecord demand, bool anyway)
        {
            int home = Gods.HomeKey(map);
            int colonists = Math.Max(1, map.mapPawns.FreeColonistsSpawnedCount);
            float sinceAsk = Since(b, home, AskMark, -1, now);
            float roll = Rand.Value;
            var priest = Worship.Priest(map);
            var people = only != null
                ? new List<Pawn> { only }
                : map.mapPawns.FreeColonistsSpawned.Where(p => Worship.Devout(p)).OrderByDescending(p => p == priest).ThenBy(_ => Rand.Value).ToList();
            foreach (var p in people)
            {
                if (!CanAsk(p, map) || only == null && !Worship.Devout(p)) continue;
                var g = onlyGod ?? Worship.GodOf(p);
                if (g == null || !GodWorshipDef.TakesOfferings(g)) continue;
                var shrine = ShrineFor(map, g);
                if (shrine == null) continue;
                float sinceGod = Since(b, home, AskGodMark, g.id, now);
                var (t, count, held) = PickPrecious(map, g, p, demand?.kind, colonists,
                    d => anyway || OfferingGiftMath.AskNow(sinceAsk, sinceGod, Since(b, home, NoMark + d.defName, g.id, now), roll));
                if (t == null) continue;
                return Send(b, map, p, g, shrine, t, count, held, demand, now);
            }
            return false;
        }

        /// <summary>
        /// The precious stack in the stores to ask for: of the god's liking (or of <paramref name="kind"/>), that the colony can
        /// spare a gift of (OfferingGiftMath.AskCount, Askable) and the asker can reach; how many, and how much the colony holds.
        /// </summary>
        private static (Thing thing, int count, int held) PickPrecious(Map map, God g, Pawn p, OfferingKind? kind, int colonists, Func<ThingDef, bool> allowed)
        {
            Thing best = null;
            int bestCount = 0, bestHeld = 0;
            float bestScore = 0f;
            var ok = new Dictionary<ThingDef, bool>();
            foreach (var group in map.haulDestinationManager.AllGroupsListForReading)
            foreach (var t in group.HeldThings)
            {
                if (t == null || !t.Spawned || t.IsForbidden(Faction.OfPlayer) || t is MinifiedThing || Offerings.Offered(t) != t) continue;
                var d = t.def;
                if (!Precious(d, map)) continue;
                var k = Offerings.Kind(d);
                if (kind.HasValue && k != kind.Value) continue;
                float fit = GodWorshipDef.Believed(g, k);
                if (fit <= 0f || !kind.HasValue && fit < 1f && g.kind != GodKind.Hunger) continue;
                if (!ok.TryGetValue(d, out bool can)) ok[d] = can = allowed(d);
                if (!can) continue;
                int held = Held(map, d);
                int count = Math.Min(OfferingGiftMath.AskCount(t.MarketValue, held, colonists), t.stackCount);
                if (!OfferingGiftMath.Askable(held, count, Keep(d, colonists))) continue;
                float score = fit * (0.8f + Rand.Value * 0.4f);
                if (score <= bestScore) continue;
                if (!p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some, 1, count)) continue;
                best = t;
                bestCount = count;
                bestHeld = held;
                bestScore = score;
            }
            return (best, bestCount, bestHeld);
        }

        /// <summary>The card: who asks to give what to which god and why, what they hope it brings, and what the colony would keep.</summary>
        private static bool Send(WorshipBook b, Map map, Pawn p, God g, Thing shrine, Thing t, int count, int held, DemandRecord demand, int now)
        {
            var d = t.def;
            string what = GenLabel.ThingLabel(d, t.Stuff, count);
            string god = Worship.Called(g, p);
            var args = new[] { p.LabelShort.Named("PAWN"), what.Named("WHAT"), god.Named("GOD"), Worship.ShrineOf(g).Named("PLACE") };
            string label = "CC_GodGift_Ask_Label".Translate(args).Resolve();
            string why = (demand != null ? "CC_GodGift_Ask_Dream" : "CC_GodGift_Ask_Why").Translate(args).Resolve();
            string hope = GodWorshipDef.For(g)?.hope;
            string hopes = hope.NullOrEmpty()
                ? "CC_GodGift_Ask_HopeAny".Translate(args).Resolve()
                : "CC_GodGift_Ask_Hope".Translate(p.LabelShort.Named("PAWN"), god.Named("GOD"), hope.Named("HOPE")).Resolve();
            string text = "CC_GodGift_Ask_Text".Translate(p.LabelShort.Named("PAWN"), what.Named("WHAT"), god.Named("GOD"), Worship.ShrineOf(g).Named("PLACE"), why.Named("WHY"),
                hopes.Named("HOPE"), held.Named("HELD"), (held - count).Named("LEFT"), d.label.Named("THING")).Resolve();
            var letter = GodAsks.Ask(GodAsks.Precious, map, g, label, text, new LookTargets(p), null, null, p, count, d.defName + (demand != null ? "|dream" : ""), -1,
                OfferingGiftMath.AskWaitDays);
            if (letter == null) return false;
            int home = Gods.HomeKey(map);
            Mark(b, home, AskMark, -1, now, now);
            Mark(b, home, AskGodMark, g.id, now, now);
            string line = Worship.Line("Rimfolk_Pool_Worship_AskGift", p, g, null, demand != null ? "dream" : null);
            if (!line.NullOrEmpty()) Speech.Say(p, line);
            return true;
        }

        private static ThingDef DefOf(string what)
        {
            if (what.NullOrEmpty()) return null;
            int bar = what.IndexOf('|');
            return DefDatabase<ThingDef>.GetNamedSilentFail(bar < 0 ? what : what.Substring(0, bar));
        }

        private static bool Dream(string what) => what != null && what.EndsWith("|dream", StringComparison.Ordinal);

        /// <summary>The card's answers: let them give it (greyed out once the colony no longer holds that much, or the god has no place here), or keep it.</summary>
        public static IEnumerable<GodAnswer> Answers(ChoiceLetter_GodWorld l)
        {
            var map = l?.HomeMap;
            var g = l?.God;
            var p = l?.pawn;
            var d = DefOf(l?.what);
            if (map == null || g == null || p == null || p.Dead || d == null || !Worship.On) yield break;
            string cant = Held(map, d) < Math.Max(1, l.count) ? "CC_GodGift_Ask_NoLonger".Translate(d.label.Named("THING")).Resolve()
                : ShrineFor(map, g) == null ? "CC_Wsh_Unavailable_NoShrine".Translate(Worship.Called(g).Named("GOD")).Resolve() : null;
            yield return new GodAnswer("CC_GodGift_Ask_Yes".Translate(p.LabelShort.Named("PAWN")).Resolve(), Yes, cant);
            yield return new GodAnswer("CC_GodGift_Ask_No".Translate().Resolve(), No);
        }

        /// <summary>Yes: they carry it to the god's place (the offerings' own carrying), the god's dream answered with it where it asked.</summary>
        private static void Yes(ChoiceLetter_GodWorld l)
        {
            var b = Worship.Book;
            var map = l.HomeMap;
            var g = l.God;
            var d = DefOf(l.what);
            var shrine = ShrineFor(map, g);
            if (b == null || map == null || g == null || d == null || shrine == null) return;
            int home = Gods.HomeKey(map);
            bool dream = Dream(l.what) && b.demands.Any(x => x != null && !x.done && !x.met && x.god == g.id && x.home == home);
            // The dream's own plan, if nobody has picked anything for it yet, gives way to this gift.
            if (dream) b.offerings.RemoveAll(o => o != null && o.demand && !o.precious && !o.set && o.carrier == null && o.god == g.id && o.home == home);
            var r = new OfferingRecord
            {
                shrine = shrine, god = g.id, home = home, chosen = Worship.Now, precious = true, want = d, wantCount = Math.Max(1, l.count), asker = l.pawn, demand = dream
            };
            b.offerings.Add(r);
            var p = l.pawn;
            if (p != null && p.Spawned && !p.Dead)
            {
                string line = Worship.Line("Rimfolk_Pool_Worship_GiftYes", p, g);
                if (!line.NullOrEmpty()) Speech.Say(p, line);
            }
            Offerings.Carry(b, r, map);
        }

        /// <summary>No: it stays in the stores; the one who asked lets it be (a little disappointed if devout), and the same ask waits a while.</summary>
        private static void No(ChoiceLetter_GodWorld l)
        {
            var b = Worship.Book;
            var map = l.HomeMap;
            var g = l.God;
            var d = DefOf(l.what);
            if (b == null || map == null || g == null) return;
            int now = Worship.Now;
            if (d != null) Mark(b, Gods.HomeKey(map), NoMark + d.defName, g.id, now, now + (int)(OfferingGiftMath.NoAgainDays * GenDate.TicksPerDay));
            var p = l.pawn;
            if (p == null || p.Dead || !Worship.Ours(p)) return;
            if (Worship.Devout(p)) CouncilGameComponent.GiveThought(p, OfferingGiftsDefOf.Rimfolk_Worship_GiftKept);
            if (!p.Spawned) return;
            string line = Worship.Line("Rimfolk_Pool_Worship_GiftNo", p, g);
            if (!line.NullOrEmpty()) Speech.Say(p, line);
        }

        // ================= Carrying =================

        /// <summary>The stack a precious gift is carried from: the largest of it in the stores the carrier can reach, and how many of it.</summary>
        internal static Thing Stack(Map map, ThingDef d, Pawn carrier, int want, out int count)
        {
            count = 0;
            if (map == null || d == null || carrier == null) return null;
            foreach (var t in map.listerThings.ThingsOfDef(d).Where(x => x.Spawned && x.IsInAnyStorage() && !x.IsForbidden(Faction.OfPlayer)).OrderByDescending(x => x.stackCount))
            {
                int n = Math.Max(1, Math.Min(want, t.stackCount));
                if (!carrier.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some, 1, n)) continue;
                count = n;
                return t;
            }
            return null;
        }

        // ================= Dev mode =================

        /// <summary>Dev mode: someone asks now, whatever the waits and the day's chance (the priest or the devout first).</summary>
        internal static bool DevAsk(Map map)
        {
            var b = Worship.Book;
            return b != null && map != null && Ask(b, map, Worship.Now, null, null, null, true);
        }

        /// <summary>Dev mode: what is precious here, of what the stores hold (the log).</summary>
        internal static string DevPrecious(Map map)
        {
            if (map == null) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var d in map.haulDestinationManager.AllGroupsListForReading.SelectMany(gr => gr.HeldThings).Where(t => t?.def != null).Select(t => t.def).Distinct().OrderBy(x => x.defName))
                sb.AppendLine("  " + d.defName + ": " + (Precious(d, map) ? "precious" : "plain") + (Renewable(d) ? "" : " (can't make or gather)"));
            return sb.ToString();
        }
    }

    [DefOf]
    public static class OfferingGiftsDefOf
    {
        public static ThoughtDef Rimfolk_Worship_GiftKept;
        public static ThoughtDef Rimfolk_Worship_OfferedPrecious;

        static OfferingGiftsDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(OfferingGiftsDefOf));
    }
}
