using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The autumn count of the herd (C21 of docs/content-update-proposals.md; the player's go of 20 November 2026). Each
    /// autumn, with four head or more, the herdmaster (else whoever is best with animals) walks the pens from animal to
    /// animal, counting (a round of the herd, PlayOut/OnMapPlays.cs). When the count is done the herd's story keeps it: the
    /// colony is told the head, by kind, against last autumn's, with the herd's leader, and the chronicle writes it down.
    /// Then the council is asked what to keep through the winter (Rimfolk_Dlm_HerdCount, a council question in the flavour
    /// style), with the hay and kibble in store set against last winter's cold as the weather record kept it (the advisor's
    /// memory, Advisor/AdvisorLastWinter.cs; the winter ahead by the climate when the record has none):
    /// <list type="bullet">
    /// <item>keep them all;</item>
    /// <item>sell the yearlings to the next traders: they are marked to sell in the trade window (Trade/TradeAdvice.cs),
    /// and when a trade caravan stops at the colony the counter goes down to the pens for them (a visit play-out); the
    /// sale is the player's, by the game's own Accept, and a choice not sold within a season lapses;</item>
    /// <item>slaughter the oldest for the winter's meat: marked for the game's own slaughter, never the herd's leader,
    /// anyone's own, bonded, named or carrying young, and a male and a female of each kind always kept.</item>
    /// </list>
    /// Saved on the herd's story (HerdStory: the count's year, the last count, the question waiting, the yearlings kept
    /// for the traders). The rules without game types are in HerdStoryMath. No harm.
    /// </summary>
    public static class HerdCount
    {
        public const string Question = "Rimfolk_Dlm_HerdCount";

        /// <summary>What the count's play-out says it is (PlayOut.afterward).</summary>
        public const string After = "herdcount";

        public const string Pool = "Rimfolk_Pool_HerdCount";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The caravans already met for the yearlings (not saved: after a load, the counter may go down once more).</summary>
        private static readonly HashSet<int> met = new HashSet<int>();
        private static Game metGame;

        /// <summary>Once an hour at a home (AnimalLife.Hourly): the count when it's due, its question, and the yearlings at the traders.</summary>
        public static void Hourly(HerdStory h, Map map)
        {
            if (h == null || map == null || !HerdStory.On || !Homes.IsHome(map) || MapFacts.InVacuum(map)) return;
            int year = GenLocalDate.Year(map);
            var herd = AnimalFacts.Herd(map);
            bool quiet = map.dangerWatcher == null || map.dangerWatcher.DangerRating == StoryDanger.None;
            if (quiet && HerdStoryMath.CountDue(GenLocalDate.Season(map) == Season.Fall, year, h.countYear, herd.Count, GenLocalDate.HourOfDay(map)))
                Start(h, map, false);
            if (h.askUntil >= 0) TryAsk(h, map);
            if (h.sellIds.Count > 0) Traders(h, map);
        }

        /// <summary>Who counts: the herdmaster, else whoever is best with animals (grown, able to handle them).</summary>
        public static Pawn Counter(Map map)
        {
            var hm = AnimalFacts.HerdmasterAt(map);
            if (hm != null && !hm.Dead && hm.Spawned && hm.Map == map && !hm.Downed) return hm;
            return map.mapPawns.FreeColonistsSpawned.Where(p => !p.DevelopmentalStage.Juvenile() && People.HasInnerLife(p) && !p.IsQuestLodger() && !p.Downed
                                                                && !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();
        }

        /// <summary>The count set going: the counter walks the pens (a round of the herd). Once a year, whether or not it comes about.</summary>
        public static string Start(HerdStory h, Map map, bool dev)
        {
            var counter = Counter(map);
            if (counter == null) return "nobody here to count the herd.";
            h.countYear = GenLocalDate.Year(map);
            var round = new DilemmaEffect_Rounds
            {
                label = "CC_HerdCount_Label".Translate().Resolve(),
                who = PlayWho.A,
                herd = true,
                count = OnMapMath.MaxRounds,
                pool = Pool,
                hours = dev ? new IntRange(0, 23) : new IntRange(HerdStoryMath.CountFrom, 17),
                duration = new FloatRange(1.5f, 2f),
                waitHours = 8f
            };
            var p = round.Begin(new DilemmaContext { map = map, a = counter });
            if (p == null) return "the count couldn't be set going (answers playing out on the map is off, or nobody to do it).";
            p.afterward = After;
            return counter.LabelShort + " goes to count the herd.";
        }

        /// <summary>
        /// The count is over (PlayAfter.Run): the herd's story keeps the year's count and its leader, the colony is told,
        /// the chronicle writes it, and the council's question waits to be asked.
        /// </summary>
        public static void Counted(PlayOut p, bool happened)
        {
            if (!happened || p?.map == null) return;
            var map = p.map;
            var h = CouncilGameComponent.Instance?.HerdOf(map);
            var counter = p.organizer ?? p.hands.FirstOrDefault()?.pawn;
            if (h == null || counter == null) return;
            try
            {
                var herd = AnimalFacts.Herd(map);
                int head = herd.Count;
                int year = GenLocalDate.Year(map);
                string kinds = Kinds(herd);
                int lastHead = h.head;
                string against = HerdStoryMath.Against(head, lastHead, h.headYear, year);
                var leader = h.leader != null && !h.leader.Dead && AnimalFacts.Ours(h.leader) ? h.leader : null;
                h.head = head;
                h.headYear = year;
                h.headLeader = leader?.LabelShort;
                var args = new[] { counter.Named("PAWN"), head.Named("HEAD"), kinds.Named("KINDS"), (leader?.LabelShort ?? "").Named("LEADER") };
                string text = (leader != null ? "CC_HerdCount_Done_Leader" : "CC_HerdCount_Done").Translate(args).Resolve();
                if (against != null) text += " " + ("CC_HerdCount_Against_" + against).Translate(lastHead.Named("HEAD")).Resolve();
                Tell.Message(NewsKind.Land, Importance.Notable, map, text, new LookTargets(herd.Take(10)), MessageTypeDefOf.NeutralEvent);
                CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Life,
                    (leader != null ? "CC_HerdCount_Chronicle_Leader" : "CC_HerdCount_Chronicle").Translate(args).Resolve(), counter));
                // The question waits for the council (asked within two days, when no other question waits).
                if (ColonyCouncilMod.Settings?.enableDilemmas ?? false) h.askUntil = Now + HerdStoryMath.AskWithinDays * GenDate.TicksPerDay;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the autumn count", e);
            }
        }

        /// <summary>"8 sheep, 5 chickens and 2 horses": the herd by kind, the most first.</summary>
        public static string Kinds(List<Pawn> herd) =>
            TextUtil.JoinAnd(herd.GroupBy(AnimalFacts.KindLabel).OrderByDescending(g => g.Count()).Take(4)
                .Select(g => g.Count() + " " + (g.Count() == 1 ? g.Key : Find.ActiveLanguageWorker.Pluralize(g.Key, g.Count()))).ToList());

        private static void TryAsk(HerdStory h, Map map)
        {
            if (Now > h.askUntil)
            {
                h.askUntil = -1;
                return;
            }
            var tracker = DilemmaTracker.For(map) ?? CouncilGameComponent.Instance?.dilemmas;
            if (tracker != null && tracker.TryFire(true, d => d.defName == Question)) h.askUntil = -1;
        }

        // ---------------- The herd's young and old ----------------

        /// <summary>One of the herd the council may part with: nobody's own, not bonded, not named by anyone, not carrying young.</summary>
        private static bool Spare(Pawn a) =>
            a != null && !a.Dead && !a.Downed && !a.IsQuestLodger() && PetTracker.PersonOf(a) == null && (a.Name == null || a.Name.Numerical)
            && !(a.relations?.DirectRelations?.Any(r => r.def == PawnRelationDefOf.Bond) ?? false) && !(a.health?.hediffSet?.HasHediff(HediffDefOf.Pregnant) ?? false);

        /// <summary>The herd's yearlings that could be sold: past their first stage of life and not yet a year old, the oldest first.</summary>
        public static List<Pawn> Yearlings(Map map) =>
            AnimalFacts.Herd(map).Where(a => Spare(a) && a.ageTracker != null && HerdStoryMath.Yearling(a.ageTracker.AgeBiologicalYearsFloat, a.ageTracker.CurLifeStageIndex))
                .OrderByDescending(a => a.ageTracker.AgeBiologicalYearsFloat).Take(HerdStoryMath.MaxSell).ToList();

        /// <summary>The oldest of the herd for the butcher: never its leader, and a male and a female of each kind always kept.</summary>
        public static List<Pawn> Oldest(Map map, int count)
        {
            var chosen = new List<Pawn>();
            if (map == null || count <= 0) return chosen;
            var herd = AnimalFacts.Herd(map);
            var leader = CouncilGameComponent.Instance?.HerdOf(map)?.leader;
            var males = herd.Where(a => a.gender == Gender.Male).GroupBy(a => a.def).ToDictionary(g => g.Key, g => g.Count());
            var females = herd.Where(a => a.gender == Gender.Female).GroupBy(a => a.def).ToDictionary(g => g.Key, g => g.Count());
            foreach (var a in herd.Where(a => a != leader && Spare(a) && a.ageTracker != null && map.designationManager.DesignationOn(a, DesignationDefOf.Slaughter) == null)
                         .OrderByDescending(a => a.ageTracker.AgeBiologicalYearsFloat))
            {
                if (chosen.Count >= count) break;
                var left = a.gender == Gender.Male ? males : a.gender == Gender.Female ? females : null;
                if (left != null)
                {
                    if (!left.TryGetValue(a.def, out int n) || n <= 1) continue;
                    left[a.def] = n - 1;
                }
                chosen.Add(a);
            }
            return chosen;
        }

        // ---------------- The yearlings at the traders ----------------

        /// <summary>One the council chose to sell at the autumn count, still kept for the traders (the trade window marks it).</summary>
        public static bool ForSale(Pawn a)
        {
            var comp = CouncilGameComponent.Instance;
            if (a == null || comp?.herds == null) return false;
            foreach (var h in comp.herds)
                if (h != null && h.sellIds.Contains(a.thingIDNumber)) return true;
            return false;
        }

        private static void Traders(HerdStory h, Map map)
        {
            if (metGame != Current.Game)
            {
                metGame = Current.Game;
                met.Clear();
            }
            // Sold, gone, dead: off the list; the chronicle keeps a sale.
            int sold = 0;
            var ours = map.mapPawns.SpawnedColonyAnimals.ToDictionary(a => a.thingIDNumber);
            foreach (int id in h.sellIds.ToList())
            {
                if (ours.ContainsKey(id)) continue;
                // Sold to a caravan still on the map, or gone off the map with it.
                var gone = map.mapPawns.AllPawnsSpawned.FirstOrDefault(x => x.thingIDNumber == id) ?? Find.WorldPawns?.AllPawnsAliveOrDead?.FirstOrDefault(x => x.thingIDNumber == id);
                if (gone != null && !gone.Dead && gone.Faction != Faction.OfPlayer) sold++;
                if (gone == null || gone.Dead || gone.Faction != Faction.OfPlayer) h.sellIds.Remove(id);
            }
            if (sold > 0) CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_HerdCount_SoldChronicle".Translate(sold.Named("COUNT")).Resolve()));
            if (h.sellIds.Count == 0)
            {
                h.sellUntil = -1;
                return;
            }
            if (Now > h.sellUntil)
            {
                Tell.Message(NewsKind.Land, Importance.Flavor, map, "CC_HerdCount_SellLapsed".Translate(h.sellIds.Count.Named("COUNT")).Resolve(), null, MessageTypeDefOf.NeutralEvent);
                h.sellIds.Clear();
                h.sellUntil = -1;
                return;
            }
            foreach (var lord in map.lordManager?.lords ?? new List<Lord>())
            {
                if (!(lord?.LordJob is LordJob_TradeWithColony) || met.Contains(lord.loadID) || lord.CurLordToil?.GetType().Name != "LordToil_DefendTraderCaravan") continue;
                met.Add(lord.loadID);
                Meet(h, map, lord.faction);
                break;
            }
        }

        /// <summary>A trade caravan has stopped at the colony: the colony is told the yearlings are marked to sell, and the counter goes down to the pens for them.</summary>
        private static void Meet(HerdStory h, Map map, Faction faction)
        {
            var young = h.sellIds.Select(id => map.mapPawns.SpawnedColonyAnimals.FirstOrDefault(a => a.thingIDNumber == id)).Where(a => a != null).ToList();
            if (young.Count == 0) return;
            var keeper = Counter(map);
            string names = TextUtil.JoinAnd(young.Select(a => a.LabelShort).ToList());
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_HerdCount_TradersHere".Translate((faction?.Name ?? "?").Named("FACTION"), names.Named("NAMES")).Resolve(),
                new LookTargets(young), MessageTypeDefOf.NeutralEvent);
            if (keeper == null) return;
            // apart: the keeper walks down to the yearlings in the pens (a visit play-out), not to anyone in particular.
            new DilemmaEffect_Visit { label = "CC_HerdCount_PickOut".Translate().Resolve(), whom = PlayWho.Pet, hours = new IntRange(6, 20), duration = new FloatRange(0.5f, 0.75f), waitHours = 6f }
                .Apply(new DilemmaContext { map = map, a = keeper, pet = young[0] });
        }

        /// <summary>The yearlings kept for the next traders (the answer "sell the yearlings").</summary>
        public static List<Pawn> KeepForSale(Map map)
        {
            var h = CouncilGameComponent.Instance?.HerdOf(map);
            var young = Yearlings(map);
            if (h == null || young.Count == 0) return young;
            h.sellIds = young.Select(a => a.thingIDNumber).ToList();
            h.sellUntil = Now + HerdStoryMath.SellDays * GenDate.TicksPerDay;
            return young;
        }

        /// <summary>Dev: a caravan stopping now (the next one counts as new), for the yearlings kept for sale.</summary>
        public static string DevTraders(Map map)
        {
            var h = CouncilGameComponent.Instance?.HerdOf(map);
            if (h == null || h.sellIds.Count == 0) return "no yearlings kept for the traders here (answer the autumn count's question to sell them).";
            Meet(h, map, Find.FactionManager.AllFactionsVisible.FirstOrDefault(f => !f.IsPlayer && !f.HostileTo(Faction.OfPlayer)));
            return "the colony is told, and the keeper goes down to the pens.";
        }

        /// <summary>Dev: the count's question now (as if the count had just been done).</summary>
        public static string DevAsk(Map map)
        {
            var h = CouncilGameComponent.Instance?.HerdOf(map);
            if (h == null) return "no herd's story here.";
            h.askUntil = Now + GenDate.TicksPerDay;
            var tracker = DilemmaTracker.For(map) ?? CouncilGameComponent.Instance?.dilemmas;
            if (tracker != null && tracker.TryFire(true, d => d.defName == Question))
            {
                h.askUntil = -1;
                return "the council is asked.";
            }
            return "the council couldn't be asked now (a question is waiting, a herd under four, or nobody to ask).";
        }
    }

    /// <summary>The autumn count's question: the head ({HEAD}), days of hay and kibble ({FEED}), last winter's cold or the winter ahead ({WINTER}), the yearlings ({YEARLINGS}), the leader ({LEADER}).</summary>
    public class HerdCountContext : DilemmaContext
    {
        public int head, feed, winter, yearlings;
        public bool lastWinter;
        public string leader;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add(head.Named("HEAD"));
            args.Add(feed.Named("FEED"));
            args.Add(winter.Named("WINTER"));
            args.Add(yearlings.Named("YEARLINGS"));
            args.Add((leader ?? "").Named("LEADER"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref head, "head");
            Scribe_Values.Look(ref feed, "feed");
            Scribe_Values.Look(ref winter, "winter");
            Scribe_Values.Look(ref yearlings, "yearlings");
            Scribe_Values.Look(ref lastWinter, "lastWinter");
            Scribe_Values.Look(ref leader, "leader");
        }
    }

    /// <summary>
    /// What the colony keeps through the winter, asked once the autumn count is done (HerdCount.Counted): A is the counter,
    /// B one with an animal of their own, else another of the grown. {COUNT} is how many of the oldest would go to the butcher.
    /// </summary>
    public class DilemmaWorker_HerdCount : DilemmaWorker
    {
        public override bool CanHappenNow(Map map)
        {
            var h = CouncilGameComponent.Instance?.HerdOf(map);
            return h != null && h.askUntil >= 0 && base.CanHappenNow(map);
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var herd = AnimalFacts.Herd(map);
            if (herd.Count < HerdStoryMath.MinHerd) return false;
            var pawns = YearFour.Grown(map);
            if (pawns.Count < def.minColonists) return false;
            var a = HerdCount.Counter(map);
            if (a == null || !pawns.Contains(a)) return false;
            var b = pawns.Where(p => p != a && map.mapPawns.SpawnedColonyAnimals.Any(x => x.playerSettings?.Master == p)).InRandomOrder().FirstOrDefault()
                    ?? VarietyThree.Other(pawns, a, SkillDefOf.Animals);
            if (b == null) return false;
            var grazers = YearFour.Grazers(map);
            float feed = YearFourMath.FeedDays(YearFour.Feed(map), YearFour.HerdADay(grazers));
            var last = AdvisorLastWinter.Of(map);
            float winter = last != null ? last.coldDays : YearFour.WinterAhead(map);
            if (winter <= 0f)
            {
                SeasonUtil.Winter(map, out _, out float length, out _);
                winter = length;
            }
            var h = CouncilGameComponent.Instance?.HerdOf(map);
            var leader = h?.leader != null && !h.leader.Dead && AnimalFacts.Ours(h.leader) ? h.leader : null;
            ctx = new HerdCountContext
            {
                def = def, map = map, a = a, b = b, head = herd.Count, feed = Mathf.Min(999, Mathf.RoundToInt(feed)), winter = Mathf.RoundToInt(winter),
                yearlings = HerdCount.Yearlings(map).Count, lastWinter = last != null, leader = leader?.LabelShort,
                count = HerdStoryMath.SlaughterCount(herd.Count, feed, winter)
            };
            // Asked now (the tracker sends what it casts): not asked again for this count.
            if (h != null) h.askUntil = -1;
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (!(ctx is HerdCountContext c)) return;
            if (c.lastWinter) tags.Add("lastwinter");
            if (c.feed < c.winter) tags.Add("short");
            if (!c.leader.NullOrEmpty()) tags.Add("leader");
        }
    }

    /// <summary>The yearlings kept for the next traders: marked to sell in the trade window, and the counter goes down to the pens for them when a caravan stops.</summary>
    public class DilemmaEffect_HerdSell : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.map != null && HerdCount.Yearlings(ctx.map).Count == 0 ? "CC_HerdCount_NoYearlings".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            var young = HerdCount.Yearlings(ctx.map);
            if (young.Count == 0) return;
            sheet.Play("CC_Impact_HerdSell".Translate(TextUtil.JoinAnd(young.Select(a => a.LabelShort).ToList()).Named("NAMES"), HerdStoryMath.SellDays.Named("DAYS")).Resolve(),
                "CC_Impact_HerdSellBrief".Translate(young.Count.Named("COUNT")).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            try
            {
                var young = HerdCount.KeepForSale(ctx.map);
                if (young.Count > 0)
                    Tell.Message(NewsKind.Land, Importance.Notable, ctx.map, "CC_HerdCount_SellMarked".Translate(TextUtil.JoinAnd(young.Select(a => a.LabelShort).ToList()).Named("NAMES")).Resolve(),
                        new LookTargets(young), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the autumn count: yearlings kept for sale", e);
            }
        }
    }

    /// <summary>The oldest of the herd marked for the game's own slaughter (the question's count), for the winter's meat.</summary>
    public class DilemmaEffect_HerdOldest : DilemmaEffect
    {
        public override bool ChanceEach => false;

        private static List<Pawn> Chosen(DilemmaContext ctx) => HerdCount.Oldest(ctx.map, Mathf.Max(1, ctx.count));

        public override string Unavailable(DilemmaContext ctx) => ctx?.map != null && Chosen(ctx).Count == 0 ? "CC_Y4_HerdNone".Translate().ToString() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.map == null) return;
            var who = Chosen(ctx);
            if (who.Count == 0) return;
            sheet.Play("CC_Impact_HerdOldest".Translate(TextUtil.JoinAnd(who.Select(a => a.LabelShort).ToList()).Named("NAMES")).Resolve(),
                "CC_Impact_SlaughterBrief".Translate(who.Count).Resolve());
            sheet.Add(ImpactKind.Goods, 1, "CC_Impact_SlaughterMeat".Translate().Resolve(), "CC_Impact_SlaughterMeatBrief".Translate().Resolve(), "slaughtermeat");
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.map == null) return;
            try
            {
                var who = Chosen(ctx);
                var dm = ctx.map.designationManager;
                foreach (var a in who)
                {
                    dm.RemoveAllDesignationsOn(a);
                    dm.AddDesignation(new Designation(a, DesignationDefOf.Slaughter));
                }
                if (who.Count > 0)
                    Tell.Message(NewsKind.Land, Importance.Notable, ctx.map, "CC_HerdCount_OldestMarked".Translate(TextUtil.JoinAnd(who.Select(a => a.LabelShort).ToList()).Named("NAMES")).Resolve(),
                        new LookTargets(who), MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the autumn count: the oldest marked", e);
            }
        }
    }
}
