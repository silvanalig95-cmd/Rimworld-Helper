using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The giving day, a winter custom (A26 of docs/content-update-proposals.md, the player's go of 20 November 2026;
    /// Rimfolk_Dilemma_GivingDay in Defs/DilemmaDefs/Rimfolk_Dilemmas_Customs_LifeVariety.xml). Someone asks that everyone
    /// give one other person something by hand. "The giving day": names are drawn so everyone gives one and gets one
    /// (GivingMath.Draw), and for each the giver's gift is chosen from the stores by what the one it's for wants or lacks (a
    /// toy or something sweet for a child, warm things in winter for someone without a hat, a book for a reader, a drink for
    /// someone who may drink, a carving; never worth more than <see cref="GivingMath.MostWorth"/>). In the afternoon each giver
    /// walks to the stores and takes theirs; the colony eats together (the one feast engine), with Vanilla Christmas
    /// Expanded's fairy lamps switched on; after supper each walks over to the one theirs is for and hands it over with a
    /// word. "The children only": each child is given something by the grown-up fondest of them. Kept three years running
    /// it is a tradition like the other customs. Every age has its gifts by what its stores hold. With the faith's festive
    /// feast (Vanilla Christmas Expanded's VCE_FestiveFeast), once the giving day is on the calendar, the gifts follow the
    /// feast instead, and that year's question isn't asked. Midwinter (Gods/Among/Midwinter.cs) hands no presents of its own
    /// on a day the giving day is kept, so the two never double. No harm.
    /// </summary>
    public static class GivingDay
    {
        public const string Pool = "Rimfolk_Pool_GivingDay_Give", ThanksPool = "Rimfolk_Pool_GivingDay_Thanks";

        private static readonly Color SpeechColor = new Color(1f, 0.9f, 0.7f);

        private static readonly string[] Toys = { "VCE_Toy_Wooden", "VCE_Toy_Cloth", "VCE_Toy_Metal" };
        private static readonly string[] Sweets = { "VCE_Gingerbread", "VCE_MilkAndCookies", "Chocolate" };
        private static readonly string[] Warm = { "VCE_Apparel_WarmHat", "VAE_Headgear_Scarf", "Apparel_Tuque", "VCE_Apparel_WarmJacket" };
        private static readonly string[] Books = { "Novel", "TextBook" };
        private static readonly string[] Drinks = { "VCE_Eggnog", "VCE_HotCocoa" };

        public static DilemmaDef Def => DefDatabase<DilemmaDef>.GetNamedSilentFail("Rimfolk_Dilemma_GivingDay");

        private static int LocalDay(Map map) => GenLocalDate.Year(map) * 60 + GenLocalDate.DayOfYear(map);

        /// <summary>The giving day is under way at this home (today, or its supper waits for its evening): midwinter hands no presents of its own.</summary>
        public static bool KeptToday(Map map)
        {
            var g = LifeVarietyBook.Instance?.giving;
            return g != null && map != null && g.map == map;
        }

        /// <summary>Kept already this year at this home (the question isn't asked again, nor the festive feast's giving).</summary>
        public static bool KeptThisYear(Map map) => map != null && LifeVarietyBook.Instance?.givingYear == GenLocalDate.Year(map);

        private static List<Pawn> Home(Map map) => map.mapPawns.FreeColonistsSpawned.Where(p => PlayOuts.OurPerson(p) && !p.IsQuestLodger()).ToList();

        // ================= Setting it going =================

        /// <summary>
        /// The giving day begins at this home: names drawn and gifts chosen from the stores. <paramref name="supperId"/> the
        /// supper's plan (-1 for none: after the festive feast, when it is handed round at once). The plan, or null.
        /// </summary>
        public static GivingPlan Start(Map map, bool childrenOnly, bool afterFeast, int supperId)
        {
            var book = LifeVarietyBook.Instance;
            if (book == null || map == null || !Homes.IsHome(map)) return null;
            var people = Home(map);
            var plan = new GivingPlan { map = map, day = LocalDay(map), start = Find.TickManager.TicksGame, childrenOnly = childrenOnly, afterFeast = afterFeast, supperId = supperId };
            var pairs = new List<(Pawn giver, Pawn to)>();
            if (childrenOnly)
            {
                var children = people.Where(p => p.DevelopmentalStage.Child()).ToList();
                var grown = people.Where(p => !p.DevelopmentalStage.Juvenile()).ToList();
                var fond = children.Select(c => grown.Select(g => g.relations == null ? -1 : Mathf.Max(-1, g.relations.OpinionOf(c))).ToArray()).ToList();
                var giverOf = GivingMath.ForChildren(fond);
                for (int i = 0; i < children.Count; i++)
                    if (giverOf[i] >= 0) pairs.Add((grown[giverOf[i]], children[i]));
            }
            else
            {
                var to = GivingMath.Draw(people.Count, Rand.Int);
                for (int i = 0; i < to.Length; i++) pairs.Add((people[i], people[to[i]]));
            }
            var taken = new Dictionary<Thing, int>();
            foreach (var (giver, receiver) in pairs)
            {
                var gift = Choose(map, giver, receiver, taken, out var kind);
                if (gift == null) continue;
                taken[gift] = (taken.TryGetValue(gift, out int n) ? n : 0) + 1;
                plan.pairs.Add(new GiftPair { giver = giver, receiver = receiver, thing = gift, label = PlayGive.Label(gift), kind = kind, state = GiftState.ToTake, since = -1 });
            }
            if (plan.pairs.Count == 0) return null;
            book.giving = plan;
            book.givingYear = GenLocalDate.Year(map);
            return plan;
        }

        /// <summary>A gift for <paramref name="to"/> from the stores, by what they want or lack: the first kind in order that the stores hold.</summary>
        private static Thing Choose(Map map, Pawn giver, Pawn to, Dictionary<Thing, int> taken, out GiftKind kind)
        {
            bool child = to.DevelopmentalStage.Juvenile();
            bool winter = GenLocalDate.Season(map) == Season.Winter || GenLocalDate.Season(map) == Season.PermanentWinter || map.mapTemperature.OutdoorTemp < 5f;
            bool bareHead = to.apparel != null && !to.apparel.WornApparel.Any(a => a.def.apparel?.layers?.Contains(ApparelLayerDefOf.Overhead) ?? false);
            bool reader = to.skills?.GetSkill(SkillDefOf.Intellectual) is SkillRecord s && !s.TotallyDisabled && People.Passionate(s);
            bool mayDrink = !child;
            foreach (var k in GivingMath.Order(child, winter && bareHead, reader, mayDrink))
            {
                var t = FindGift(map, giver, to, k, taken);
                if (t == null) continue;
                kind = k;
                return t;
            }
            kind = GiftKind.Carving;
            return null;
        }

        private static IEnumerable<ThingDef> DefsOf(GiftKind k)
        {
            string[] names;
            switch (k)
            {
                case GiftKind.Toy: names = Toys; break;
                case GiftKind.Sweet: names = Sweets; break;
                case GiftKind.Warm: names = Warm; break;
                case GiftKind.Book: names = Books; break;
                case GiftKind.Drink: names = Drinks; break;
                default: names = new string[0]; break;
            }
            foreach (var n in names)
            {
                var d = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (d != null) yield return d;
            }
        }

        /// <summary>A stack in the colony's stores of this kind of gift, that the giver can reach and the one it's for may have.</summary>
        private static Thing FindGift(Map map, Pawn giver, Pawn to, GiftKind k, Dictionary<Thing, int> taken)
        {
            bool Ok(Thing t) => t != null && t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(giver) && t.stackCount > (taken.TryGetValue(t, out int n) ? n : 0)
                                && t.MarketValue <= GivingMath.MostWorth && giver.CanReach(t, PathEndMode.ClosestTouch, Danger.Some)
                                && !(t.def.IsDrug && Beliefs.MindsDrug(to, t.def, false));
            if (k == GiftKind.Carving)
            {
                var carving = DefDatabase<ThingDef>.GetNamedSilentFail("SculptureSmall");
                return map.listerThings.ThingsOfDef(ThingDefOf.MinifiedThing).FirstOrDefault(t => t is MinifiedThing m && m.InnerThing?.def == carving && Ok(t));
            }
            foreach (var d in DefsOf(k))
            {
                var t = map.listerThings.ThingsOfDef(d).FirstOrDefault(Ok);
                if (t != null) return t;
            }
            if (k == GiftKind.Drink)
            {
                // Any coffee or tea a mod adds (Vanilla Brewing Expanded - Coffees and Teas), then the game's beer for someone who may drink it.
                var cup = map.listerThings.AllThings.FirstOrDefault(t => t.def.IsIngestible && LifeNewTracker.IsCup(t.def) && Ok(t));
                if (cup != null) return cup;
                var beer = DefDatabase<ThingDef>.GetNamedSilentFail("Beer");
                if (beer != null) return map.listerThings.ThingsOfDef(beer).FirstOrDefault(Ok);
            }
            return null;
        }

        // ================= The hours =================

        /// <summary>Once an hour: the fairy lamps at supper, the giving after it, the end of the evening.</summary>
        public static void Hourly(LifeVarietyBook book)
        {
            var g = book.giving;
            var map = g?.map;
            int now = Find.TickManager.TicksGame;
            if (g == null || map == null || !Homes.IsHome(map) || now - g.start > GenDate.TicksPerHour * GivingMath.MostHours)
            {
                End(book);
                return;
            }
            int hour = GenLocalDate.HourOfDay(map);
            // Handed round and the evening over (the supper done and midnight come): the day is told.
            if (SupperOver(g) && GivenOut(g) && (hour < 6 || hour > GivingMath.EveningEnds))
            {
                End(book);
                return;
            }
            if (!g.lit && !g.afterFeast && hour >= 17 && SupperStarted(g) && !Midwinter.KeepingToday(map)) Lamps(g, map);
        }

        /// <summary>Taking the gifts from the stores (from the afternoon), and handing them round (after supper).</summary>
        public static void Step(LifeVarietyBook book, int now)
        {
            var g = book.giving;
            var map = g?.map;
            if (g == null || map == null || MapFacts.ThreatOn(map)) return;
            int hour = GenLocalDate.HourOfDay(map);
            bool takeTime = g.afterFeast || hour >= GivingMath.TakeFrom || SupperStarted(g);
            bool giveTime = g.afterFeast || (SupperOver(g) && (hour >= GivingMath.GiveFrom || hour < 6));
            foreach (var pair in g.pairs)
            {
                try
                {
                    if (pair.state == GiftState.ToTake && takeTime) Take(pair, now);
                    else if (pair.state == GiftState.Carried && giveTime) Give(pair, now);
                }
                catch (Exception e)
                {
                    pair.state = GiftState.Lost;
                    CouncilLog.ErrorOnce("colony life, part 2: a gift of the giving day", e);
                }
            }
            if (GivenOut(g) && g.afterFeast) End(book);
        }

        private static bool GivenOut(GivingPlan g) => g.pairs.All(p => p.state == GiftState.Given || p.state == GiftState.Lost);

        /// <summary>The supper is under way or over.</summary>
        private static bool SupperStarted(GivingPlan g)
        {
            if (g.supperId < 0) return true;
            var p = PlayOutTracker.Instance?.Get(g.supperId);
            return p == null || p.state != PlayState.Pending;
        }

        /// <summary>The supper is over (or there is none, or it fell through): the gifts may be handed round.</summary>
        private static bool SupperOver(GivingPlan g)
        {
            if (g.supperId < 0) return true;
            var p = PlayOutTracker.Instance?.Get(g.supperId);
            return p == null || p.state == PlayState.Done;
        }

        private static bool Free(Pawn p) => p != null && p.Spawned && !p.Dead && Together.Able(p) && WorshipJobs.Free(p, false);

        /// <summary>The giver walks to the stores and takes their gift into their pack.</summary>
        private static void Take(GiftPair pair, int now)
        {
            var giver = pair.giver;
            var stack = pair.thing;
            if (giver == null || giver.Dead || stack == null || stack.Destroyed || !stack.Spawned || stack.Map != giver.Map)
            {
                pair.state = GiftState.Lost;
                return;
            }
            if (pair.since < 0) pair.since = now;
            if (now - pair.since > GenDate.TicksPerHour * 4)
            {
                pair.state = GiftState.Lost;
                return;
            }
            if (giver.Position.InHorDistOf(stack.Position, 1.9f))
            {
                var part = stack.SplitOff(1);
                if (!giver.inventory.innerContainer.TryAdd(part))
                {
                    GenPlace.TryPlaceThing(part, giver.Position, giver.Map, ThingPlaceMode.Near);
                    pair.state = GiftState.Lost;
                    return;
                }
                pair.thing = part;
                pair.label = PlayGive.Label(part);
                pair.state = GiftState.Carried;
                pair.since = now;
                return;
            }
            if (!Free(giver) || giver.CurJobDef == JobDefOf.Goto) return;
            OwnHappenings.GoTo(giver, stack.Position, GenDate.TicksPerHour);
        }

        /// <summary>The giver walks over to the one it's for and hands it over, with a word.</summary>
        private static void Give(GiftPair pair, int now)
        {
            var giver = pair.giver;
            var to = pair.receiver;
            var gift = pair.thing;
            if (giver == null || giver.Dead || to == null || to.Dead || gift == null || gift.Destroyed || !giver.inventory.innerContainer.Contains(gift))
            {
                pair.state = GiftState.Lost;
                return;
            }
            if (!to.Spawned || to.Map != giver.Map || to.Downed || !to.Awake()) return;
            if (Together.Can(giver, to))
            {
                if (giver.inventory.innerContainer.TryTransferToContainer(gift, to.inventory.innerContainer, gift.stackCount) <= 0) return;
                pair.state = GiftState.Given;
                // together: the giver walked over to them (Together.Can), and Speech.Say checks it again.
                string line = Flavor.FromPool(Pool, giver, null, null, "gift:" + pair.kind.ToString().ToLowerInvariant(), to.DevelopmentalStage.Juvenile() ? "forchild" : "foradult");
                if (!line.NullOrEmpty()) Speech.Say(giver, line.Formatted(giver.Named("PAWN"), to.Named("OTHER"), pair.label.Named("GIFT")).Resolve(), to, SpeechColor);
                if (Rand.Chance(0.5f))
                {
                    string thanks = Flavor.FromPool(ThanksPool, to);
                    if (!thanks.NullOrEmpty()) Speech.Say(to, thanks.Formatted(to.Named("PAWN"), giver.Named("OTHER"), pair.label.Named("GIFT")).Resolve(), giver, SpeechColor);
                }
                var mood = DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_GivingDayGift");
                if (mood != null) CouncilGameComponent.GiveThought(to, mood, giver);
                CouncilGameComponent.Instance?.social?.Bump(giver, to, TieKind.Friends, 3f, "CC_Life2_GiftNote".Translate(giver.Named("PAWN"), to.Named("OTHER"), pair.label.Named("GIFT")).Resolve());
                LifeVarietyBook.Instance?.NoteGift(giver, to, pair.label);
                return;
            }
            if (pair.since < 0) pair.since = now;
            if (!Free(giver)) return;
            if (giver.CurJobDef != JobDefOf.Goto || !giver.pather.Destination.Cell.InHorDistOf(to.Position, 3f))
                OwnHappenings.GoTo(giver, CellFinder.StandableCellNear(to.Position, to.Map, 2f), GenDate.TicksPerHour);
        }

        /// <summary>Supper: Vanilla Christmas Expanded's fairy lamps switched on, unless midwinter already lit them today.</summary>
        private static void Lamps(GivingPlan g, Map map)
        {
            g.lit = true;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("VCE_FairyLamps");
            if (def == null) return;
            int lit = 0;
            foreach (var b in map.listerBuildings.allBuildingsColonist.Where(x => x != null && x.Spawned && x.def == def))
            {
                var f = b.TryGetComp<CompFlickable>();
                if (f == null || f.SwitchIsOn) continue;
                Traverse.Create(f).Field("wantSwitchOn").SetValue(true);
                f.SwitchIsOn = true;
                lit++;
            }
            if (lit > 0)
                Tell.Minor(NewsKind.Colony, Importance.Flavor, map, "CC_Life2_GivingLamps".Translate(lit.Named("COUNT")).Resolve(), null, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>The evening is over: what wasn't handed round is set down where its giver stands, and the day told.</summary>
        private static void End(LifeVarietyBook book)
        {
            var g = book.giving;
            book.giving = null;
            if (g == null || g.told) return;
            g.told = true;
            foreach (var pair in g.pairs.Where(p => p.state == GiftState.Carried))
            {
                var giver = pair.giver;
                if (giver == null || giver.Dead || pair.thing == null || !giver.inventory.innerContainer.Contains(pair.thing)) continue;
                if (giver.Spawned) giver.inventory.innerContainer.TryDrop(pair.thing, giver.Position, giver.Map, ThingPlaceMode.Near, out _);
            }
            var given = g.pairs.Where(p => p.state == GiftState.Given).ToList();
            if (given.Count == 0 || g.map == null) return;
            Tell.Minor(NewsKind.Colony, Importance.Notable, g.map, "CC_Life2_GivingOver".Translate(given.Count.Named("COUNT")).Resolve(),
                new LookTargets(given.Select(p => p.receiver).Where(p => p != null && p.Spawned)), MessageTypeDefOf.PositiveEvent);
            var comp = CouncilGameComponent.Instance;
            foreach (var p in given.Take(4))
                comp?.journals.WriteText(p.receiver, "CC_Life2_GivingJournal".Translate(p.receiver.Named("PAWN"), p.giver.Named("OTHER"), p.label.Named("GIFT")).Resolve());
        }

        // ================= The festive feast =================

        /// <summary>
        /// The faith's festive feast (Vanilla Christmas Expanded) was held to its end: once the giving day is on the calendar and
        /// not kept yet this year, the gifts follow the feast (names drawn, taken and handed round at once).
        /// </summary>
        public static void AfterFestiveFeast(Precept_Ritual ritual, Map map)
        {
            if (ritual?.def?.defName != "VCE_FestiveFeast" || map == null || !PlayOuts.On || LifeVarietyBook.Instance?.giving != null) return;
            var def = Def;
            var cal = ColonyCalendar.For(map);
            if (def == null || cal?.CustomOf(def) == null || KeptThisYear(map)) return;
            var plan = Start(map, false, true, -1);
            if (plan != null)
                Tell.Minor(NewsKind.Colony, Importance.Notable, map, "CC_Life2_GivingAfterFeast".Translate(plan.pairs.Count.Named("COUNT")).Resolve(), null, MessageTypeDefOf.PositiveEvent);
        }

        // ================= Dev =================

        public static string DevStart(Map map, bool childrenOnly)
        {
            var book = LifeVarietyBook.Instance;
            if (book == null || map == null) return "no game.";
            book.giving = null;
            var plan = Start(map, childrenOnly, false, -1);
            if (plan == null) return "nothing to give: nobody at home, or nothing in the stores worth giving.";
            return "the giving day: " + string.Join("; ", plan.pairs.Select(p => p.giver.LabelShort + " gives " + p.receiver.LabelShort + " the " + p.label)) + ". Taken from "
                   + GivingMath.TakeFrom + " o'clock, handed round from " + GivingMath.GiveFrom + " (no supper set going here).";
        }
    }

    /// <summary>The giving day kept (A26): the supper, and the names drawn and the gifts chosen for after it (<see cref="GivingDay"/>).</summary>
    public class DilemmaEffect_GivingDay : DilemmaEffect_Feast
    {
        /// <summary>Only the children are given to; the supper is still everyone's (the base's childrenOnly would seat only them).</summary>
        public bool giftsToChildren;

        public override string Unavailable(DilemmaContext ctx) =>
            giftsToChildren && ctx?.map != null && !ctx.map.mapPawns.FreeColonistsSpawned.Any(p => p.DevelopmentalStage.Child()) ? "CC_Life2_NoChildren".Translate().Resolve() : null;

        protected override string Extra(DilemmaContext ctx) => (giftsToChildren ? "CC_Life2_Giving_ImpactChildren" : "CC_Life2_Giving_ImpactAll").Translate().Resolve();

        protected override PlayOut Create(DilemmaContext ctx)
        {
            var p = base.Create(ctx);
            if (p != null) GivingDay.Start(ctx.map, giftsToChildren, false, p.id);
            return p;
        }
    }

    /// <summary>The giving day's question: winter, once a year at a home, never on a day the gifts were already handed round.</summary>
    public class DilemmaWorker_GivingDay : DilemmaWorker_Custom
    {
        public override bool CanHappenNow(Map map) => base.CanHappenNow(map) && !GivingDay.KeptThisYear(map) && LifeVarietyBook.Instance?.giving == null;
    }
}
