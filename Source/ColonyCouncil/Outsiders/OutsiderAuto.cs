using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// The right-click's things done by the colonists themselves (the player's request, 7 November 2026: "i'm not really
    /// using the right click conversation options ... integrate them to be done automatically by pawns when there is a
    /// good moment, while still having the gameplay impact and limits in mind"). Every 1,000 ticks on each home with no
    /// danger: someone from outside who is free and not on their way, a colonist idle or at leisure near them, and the
    /// thing that would bring most now (OutsiderAutoMath.Worth) done by whoever suits it best (WhoScore). It goes the
    /// right-click's own way: the same reasons why not (Outsiders.WhyNot), the same talk job (Outsiders.StartTalk, the
    /// gift job), so the waits, the outsider's patience, "asked already" and the pushy ask count the same for both; only
    /// not as the player's order (the job's playerForced), so a turning away and the everyday outcomes are said in bubbles
    /// only. Its own pace on top (OutsiderAutoMath.PaceAllows): a few a day, two with one outsider, never their last
    /// patience, and a small budget of the stores a day. Which things may happen on their own is the setting
    /// (<see cref="CouncilSettings.outsiderAuto"/>, OutsiderAutoMath.Tier). The pace is saved on the book and the visits;
    /// who started one lately is the session's only, like the talks of their own.
    /// </summary>
    public static class OutsiderAuto
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Which things the colonists do by themselves (the setting), nothing while talking with outsiders is off.</summary>
        public static OutsiderAutoMode Mode => !Outsiders.On ? OutsiderAutoMode.Off : S?.outsiderAuto ?? OutsiderAutoMode.WithinLimits;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>When each colonist last began one by themselves, and how many on which day; who began the last one.</summary>
        private static readonly Dictionary<int, int> lastBy = new Dictionary<int, int>();
        private static readonly Dictionary<int, (int day, int n)> todayBy = new Dictionary<int, (int, int)>();
        private static Pawn lastOne;

        public static void Reset()
        {
            lastBy.Clear();
            todayBy.Clear();
            lastOne = null;
        }

        private const string Speaker = "Council_Office_Speaker", Steward = "Council_Office_Steward", Warden = "Council_Office_Warden", Chaplain = "Council_Office_Chaplain";

        // ================= The look =================

        /// <summary>From the outsiders' 250-tick look (OutsiderBook), every fourth: one thing at most, on the first home with a moment for it.</summary>
        public static void Rare(int now)
        {
            var mode = Mode;
            if (mode == OutsiderAutoMode.Off || now / Sched.RareTicks % OutsiderAutoMath.LookEvery != 0) return;
            var book = OutsiderBook.Instance;
            if (book == null) return;
            book.AutoDay(now);
            if (book.autoToday >= OutsiderAutoMath.PerDay || !OutsiderAutoMath.Waited(book.autoLast, now, OutsiderAutoMath.GapHours)) return;
            foreach (var map in Homes.All)
                if (TryOn(map, now, mode, book))
                    return;
        }

        /// <summary>
        /// A dev mode look (OutsiderAutoDebug): what it weighs written down, with or without the pace and the chance, and
        /// with or without beginning the thing chosen.
        /// </summary>
        internal sealed class Trial
        {
            public readonly StringBuilder log = new StringBuilder();
            /// <summary>No chance to pass and no pace of the day or with them (their last patience is still kept).</summary>
            public bool force;
            /// <summary>Only say what would begin.</summary>
            public bool dry;
        }

        internal static bool TryOn(Map map, int now, OutsiderAutoMode mode, OutsiderBook book, Trial trial = null)
        {
            if (map == null || !map.IsPlayerHome || map.dangerWatcher.DangerRating != StoryDanger.None || MapFacts.ThreatOn(map))
            {
                trial?.log.AppendLine("  Not a home, or danger or a threat on the map.");
                return false;
            }
            // Outsiders someone of the colony is already on the way to, or with.
            HashSet<Pawn> sought = null;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
                if (c.CurJob != null && OursWith(c.CurJobDef) && c.CurJob.targetA.Thing is Pawn t)
                    (sought ??= new HashSet<Pawn>()).Add(t);
            List<Pawn> outsiders = null;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == null || p.Faction == Faction.OfPlayer || !Outsiders.IsOutsider(p)) continue;
                if (FreeOutsider(p, sought)) (outsiders ??= new List<Pawn>()).Add(p);
                else trial?.log.AppendLine($"  {p.LabelShort}: not free ({Outsiders.Busy(p, null, OutsiderAct.Chat) ?? (Passing(p) ? "passing through" : "sought, in a scene or busy with their own")}).");
            }
            if (outsiders == null) return false;
            if (trial == null && !Rand.Chance(OutsiderAutoMath.TakeChance)) return false;
            var colonists = map.mapPawns.FreeColonistsSpawned.Where(c => FreeColonist(c, now)).ToList();
            trial?.log.AppendLine($"  Free colonists: {(colonists.Count == 0 ? "none" : string.Join(", ", colonists.Select(c => c.LabelShort)))}.");
            if (colonists.Count == 0) return false;
            outsiders.Shuffle();
            int day = OutsiderAutoMath.Day(now), weighed = 0;
            foreach (var x in outsiders)
            {
                var v = OutsiderTalk.VisitOf(x, false);
                var pace = new AutoPace
                {
                    now = now, today = book.autoToday, last = book.autoLast, withThem = v?.AutosOn(day) ?? 0, lastWithThem = v?.autoLast ?? -1,
                    patienceLeft = OutsiderTalk.Left(x), chatterLast = v?.chatterLast ?? -1
                };
                bool paced = trial != null && trial.force ? pace.patienceLeft > OutsiderAutoMath.PatienceKept : OutsiderAutoMath.PaceAllows(pace);
                trial?.log.AppendLine($"  {x.LabelShort}: patience left {pace.patienceLeft}, {pace.withThem} by themselves with them today{(paced ? "" : ", held back by the pace")}.");
                if (!paced) continue;
                if (TryWith(map, x, colonists, now, mode, book, trial)) return true;
                if (trial == null && ++weighed >= OutsiderAutoMath.OutsidersALook) break;
            }
            return false;
        }

        /// <summary>A job of the colony's people with an outsider (a talk, a gift, a lesson, a meal, a bout).</summary>
        private static bool OursWith(JobDef d) =>
            d == OutsiderDefOf.Council_OutsiderTalk || d == OutsiderDefOf.Council_OutsiderGift || d == OutsiderDefOf.Council_OutsiderLesson
            || d == OutsiderDefOf.Council_OutsiderMeal || d == OutsiderDefOf.Council_OutsiderBout;

        // ================= The moment =================

        /// <summary>Free for it: grown, up and able to talk, not busy with their own, not leaving or passing through, not trading, in no scene, nobody already on the way to them.</summary>
        private static bool FreeOutsider(Pawn x, HashSet<Pawn> sought)
        {
            if (x.DevelopmentalStage.Juvenile() || !Together.Able(x)) return false;
            if (!(x.health?.capacities?.CapableOf(PawnCapacityDefOf.Talking) ?? false)) return false;
            bool busy = Outsiders.Busy(x, null, OutsiderAct.Chat) != null || x.CurJobDef == OutsiderDefOf.Council_OutsiderListen
                        || x.CurJobDef == OutsiderDefOf.Council_OutsiderBring || x.CurJobDef == OutsiderDefOf.Council_OutsiderTeach;
            return OutsiderAutoMath.OutsiderFree(busy, Outsiders.Leaving(x), Passing(x), Outsiders.TradingNow(x), OutsiderTalk.Busy(x), sought != null && sought.Contains(x));
        }

        /// <summary>On the move across the map: their group travelling in or through (the game's travel toils), not stopping yet.</summary>
        private static bool Passing(Pawn x)
        {
            try
            {
                string duty = x.mindState?.duty?.def?.defName ?? "";
                if (duty.Contains("Travel")) return true;
                var toil = x.GetLord()?.CurLordToil;
                return toil != null && toil.GetType().Name.Contains("Travel");
            }
            catch
            {
                // Some other mod's lord: don't hold anyone up for it.
                return false;
            }
        }

        /// <summary>
        /// Free for it: grown, able to talk, of no gathering, awake and up, not drafted or in a mental state, idle,
        /// wandering or at leisure (or in their recreation time at anything they'd leave for a word), not needing to eat or
        /// sleep, in no scene, and a while since they last did one by themselves.
        /// </summary>
        private static bool FreeColonist(Pawn c, int now)
        {
            if (c == null || c.DevelopmentalStage.Juvenile() || c.IsQuestLodger() || c.GetLord() != null) return false;
            if (!(c.health?.capacities?.CapableOf(PawnCapacityDefOf.Talking) ?? false)) return false;
            if (OutsiderTalk.Busy(c)) return false;
            if (lastBy.TryGetValue(c.thingIDNumber, out int t) && !OutsiderAutoMath.ColonistRested(t, now)) return false;
            if (!PlayOuts.CanJoin(c) || !PlayOuts.Interruptible(c)) return false;
            return OutsiderAutoMath.ColonistFree(c.Awake(), c.Downed, c.Drafted, c.InMentalState, c.CurJob?.playerForced ?? false, AtLeisure(c), PlayOuts.NeedsPress(c));
        }

        private static bool AtLeisure(Pawn c)
        {
            var d = c.CurJob?.def;
            if (d == null || d.joyKind != null) return true;
            if (d == JobDefOf.Wait_Wander || d == JobDefOf.GotoWander || d == JobDefOf.Wait || d == JobDefOf.Wait_MaintainPosture) return true;
            return c.timetable?.CurrentAssignment == TimeAssignmentDefOf.Joy;
        }

        // ================= The thing and the colonist =================

        private struct Pick
        {
            public float score;
            public Pawn c;
            public OutsiderAct act;
        }

        /// <summary>
        /// For one outsider: each thing the setting allows, with each of the nearest free colonists, weighed by what it
        /// would bring and who suits it; the best few checked in full by the right-click's own reasons, the first that
        /// passes begun.
        /// </summary>
        private static bool TryWith(Map map, Pawn x, List<Pawn> colonists, int now, OutsiderAutoMode mode, OutsiderBook book, Trial trial)
        {
            var near = colonists.Where(c => c.Position.InHorDistOf(x.Position, OutsiderAutoMath.Range))
                .OrderBy(c => c.Position.DistanceToSquared(x.Position)).Take(OutsiderAutoMath.NearestColonists).ToList();
            if (near.Count == 0)
            {
                trial?.log.AppendLine($"    No free colonist within {OutsiderAutoMath.Range:0} cells.");
                return false;
            }
            int ofColony = Outsiders.OfColony(x);
            var f = Outsiders.PeopleOf(x);
            int goodwill = f != null && f.HasGoodwill ? f.PlayerGoodwill : 0;
            bool notable = Outsiders.NotableOf(x) != null;
            bool heard = HeardLately(book, x, now);
            var v = OutsiderTalk.VisitOf(x, false);
            bool firstGift = (v?.gifts ?? 0) == 0;
            int day = OutsiderAutoMath.Day(now);
            var picks = new List<Pick>();
            foreach (var c in near)
            {
                int ofColonist = Outsiders.OfColonist(x, c);
                if (OutsiderMath.Shuns(ofColony, ofColonist)) continue;
                var pair = book.Pair(c, x, false);
                // Never pushy: one who asked them something lately leaves the next ask to someone else.
                bool askedLately = OutsiderTalk.AsksBy(x, c) > 0;
                string office = S != null && S.offices ? OfficeTracker.HeldBy(c)?.defName : null;
                var social = c.skills?.GetSkill(SkillDefOf.Social);
                foreach (var act in OutsiderAutoMath.Order)
                {
                    if (!OutsiderAutoMath.Allowed(mode, act) || !Outsiders.Shown(c, x, act)) continue;
                    if (askedLately && OutsiderTalkMath.Asks(act)) continue;
                    // The right-click's own wait, for every thing (an ask asked again only gets "you asked me that already").
                    int last = OutsiderMath.PerOutsider(act) ? book.LastWith(x, act) : pair?.Last(act) ?? -1;
                    if (!OutsiderMath.Waited(last, now, act)) continue;
                    var o = new AutoOption
                    {
                        act = act, ofColony = ofColony, ofColonist = ofColonist, warmth = pair?.warmth ?? 0f, familiarity = pair?.fam ?? 0f, visits = pair?.visits ?? 0,
                        friends = pair?.friends ?? false, sweethearts = (pair?.court ?? 0) == 1, goodwill = goodwill, heardLately = heard, notable = notable,
                        firstGift = firstGift, giftFit = 1.5f
                    };
                    var who = new AutoWho
                    {
                        act = act, social = social?.Level ?? 0, ofColonist = ofColonist, familiarity = o.familiarity, notable = notable,
                        speakerOrSteward = office == Speaker || office == Steward, warden = office == Warden, chaplain = office == Chaplain, shy = Shy(c),
                        distance = c.Position.DistanceTo(x.Position), wasLast = c == lastOne, today = TodayBy(c, day)
                    };
                    if (!Fill(c, x, act, ref o, ref who)) continue;
                    float worth = OutsiderAutoMath.Worth(o);
                    if (worth < 0f) continue;
                    float whoScore = OutsiderAutoMath.WhoScore(who);
                    picks.Add(new Pick { score = OutsiderAutoMath.Score(worth, whoScore), c = c, act = act });
                    trial?.log.AppendLine($"    {act} by {c.LabelShort}: worth {worth:0.0}, who {whoScore:0.0}, score {OutsiderAutoMath.Score(worth, whoScore):0.0}.");
                }
            }
            if (picks.Count == 0)
            {
                trial?.log.AppendLine("    Nothing worth doing with them now (waits, the setting, or nothing that fits).");
                return false;
            }
            foreach (var p in picks.OrderByDescending(p => p.score).Take(OutsiderAutoMath.FullChecks))
            {
                string why = Outsiders.WhyNot(p.c, x, p.act) ?? (OutsiderTalk.Repeat(p.c, x, p.act, out _) ? "asked already" : null);
                if (why != null)
                {
                    trial?.log.AppendLine($"    {p.act} by {p.c.LabelShort}: not now ({why}).");
                    continue;
                }
                if (!Ready(map, p.c, x, p.act, book, out var gift, out int giftCount, out float cost))
                {
                    trial?.log.AppendLine($"    {p.act} by {p.c.LabelShort}: not from the stores now (meals or drinks to spare, a gift that fits the budget, the cap on hurts, a bout today).");
                    continue;
                }
                if (trial != null && trial.dry)
                {
                    trial.log.AppendLine($"    Would begin: {p.act} by {p.c.LabelShort}{(gift != null ? ", " + giftCount + " " + gift.LabelNoCount : "")}, {cost:0} silver of the stores.");
                    return true;
                }
                bool began = p.act == OutsiderAct.Gift ? Outsiders.StartGift(p.c, x, gift, giftCount, true) : Outsiders.StartTalk(p.c, x, p.act, byThemselves: true);
                trial?.log.AppendLine($"    {(began ? "Began" : "Couldn't begin")}: {p.act} by {p.c.LabelShort}.");
                if (!began) continue;
                Began(p.c, x, p.act, cost, book, now);
                return true;
            }
            return false;
        }

        /// <summary>What each thing needs to be weighed: the facts of the moment for it (false: not for these two now).</summary>
        private static bool Fill(Pawn c, Pawn x, OutsiderAct act, ref AutoOption o, ref AutoWho who)
        {
            switch (act)
            {
                case OutsiderAct.Faith:
                    o.sameFaith = c.Ideo != null && c.Ideo == x.Ideo;
                    return o.sameFaith;
                case OutsiderAct.Court:
                {
                    // Only a courtship the player began (sweethearts), and never with someone else in either's life.
                    if (!o.sweethearts || LovePartnerRelationUtility.HasAnyLovePartner(c) || LovePartnerRelationUtility.HasAnyLovePartner(x)) return false;
                    float game;
                    try
                    {
                        game = InteractionWorker_RomanceAttempt.SuccessChance(c, x, 0.8f);
                    }
                    catch
                    {
                        game = 0f;
                    }
                    o.courtChance = OutsiderMath.CourtChance(game, o.familiarity);
                    return true;
                }
                case OutsiderAct.Teach:
                {
                    var skill = Outsiders.TeachSkill(c, x);
                    if (skill == null) return false;
                    var mine = c.skills?.GetSkill(skill);
                    int lead = (x.skills?.GetSkill(skill)?.Level ?? 0) - (mine?.Level ?? 0);
                    o.passion = who.passion = People.PassionLevel(mine);
                    o.lead = who.lead = lead;
                    return true;
                }
                case OutsiderAct.Spar:
                {
                    int melee = Outsiders.Melee(c);
                    bool keen = People.PassionLevel(c.skills?.GetSkill(SkillDefOf.Melee)) > 0 || Outsiders.Temper(c) >= 1;
                    o.keen = who.keen = keen;
                    who.melee = melee;
                    o.sparChance = OutsiderMath.SparAcceptChance(Outsiders.Temper(x), Outsiders.Melee(x), melee) * (o.ofColonist < -10 ? 0.6f : 1f);
                    return true;
                }
                case OutsiderAct.Meal:
                    o.food = who.food = c.needs?.food?.CurLevelPercentage ?? 1f;
                    return true;
                case OutsiderAct.Drink:
                    o.joy = who.joy = c.needs?.joy?.CurLevelPercentage ?? 1f;
                    o.joyTime = c.timetable?.CurrentAssignment == TimeAssignmentDefOf.Joy;
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>
        /// What the stores give for it, within the day's budget, and the last checks that cost a look at the stores: a meal
        /// and the meals to spare, a drink their drug policy allows, a gift that suits them from plenty, a bout within the
        /// cap on hurts. <paramref name="cost"/> is what it spends of the stores, in silver.
        /// </summary>
        private static bool Ready(Map map, Pawn c, Pawn x, OutsiderAct act, OutsiderBook book, out Thing gift, out int giftCount, out float cost)
        {
            gift = null;
            giftCount = 0;
            cost = 0f;
            float wealth = map.wealthWatcher?.WealthTotal ?? 0f;
            switch (act)
            {
                case OutsiderAct.Meal:
                {
                    if (!OutsiderAutoMath.MealsToSpare(MealsStored(map), map.mapPawns.FreeColonistsSpawnedCount)) return false;
                    if (!Outsiders.Portions(c, x, false, out _, out var forX)) return false;
                    // The colonist would eat anyway: their guest's portion is what it spends.
                    cost = forX.MarketValue;
                    return OutsiderAutoMath.Affords(cost, wealth, book.autoSpent);
                }
                case OutsiderAct.Drink:
                {
                    if (Teetotal(c) || !Outsiders.Portions(c, x, true, out var forC, out var forX)) return false;
                    if (Beliefs.MindsDrug(c, forC.def) || Stored(map, forC.def) < OutsiderAutoMath.DrinksKept) return false;
                    cost = forC.MarketValue + forX.MarketValue;
                    return OutsiderAutoMath.Affords(cost, wealth, book.autoSpent);
                }
                case OutsiderAct.Gift:
                {
                    float left = OutsiderAutoMath.BudgetLeft(wealth, book.autoSpent);
                    float bestFit = 0f, bestValue = 0f;
                    foreach (var g in Outsiders.GiftChoices(c, x))
                    {
                        var t = g.thing;
                        if (t == null || g.against || !g.suits || !GiftKind(t)) continue;
                        float unit = t.MarketValue;
                        int n = OutsiderAutoMath.GiftCount(unit, t.stackCount, left);
                        if (n <= 0 || !OutsiderAutoMath.GiftFromPlenty(n, Stored(map, t.def))) continue;
                        float value = unit * n;
                        // What suits them best, then what spends least.
                        if (gift != null && (g.fit < bestFit || g.fit == bestFit && value >= bestValue)) continue;
                        gift = t;
                        giftCount = n;
                        bestFit = g.fit;
                        bestValue = value;
                    }
                    cost = bestValue;
                    return gift != null;
                }
                case OutsiderAct.Spar:
                    // One bout a day by themselves, and only while neither has had a hurt of this kind lately (Harm's shared cap).
                    return book.autoBoutDay != OutsiderAutoMath.Day(Now) && Harm.May(c) && Harm.May(x);
                default:
                    return true;
            }
        }

        /// <summary>It began: the pace and the budget counted, on the book (the day), their visit (two a day with them), and the colonist.</summary>
        private static void Began(Pawn c, Pawn x, OutsiderAct act, float cost, OutsiderBook book, int now)
        {
            int day = OutsiderAutoMath.Day(now);
            book.autoToday++;
            book.autoLast = now;
            book.autoSpent += Math.Max(0f, cost);
            if (act == OutsiderAct.Spar) book.autoBoutDay = day;
            var v = OutsiderTalk.VisitOf(x, true);
            if (v != null)
            {
                if (v.autoDay != day)
                {
                    v.autoDay = day;
                    v.autos = 0;
                }
                v.autos++;
                v.autoLast = now;
            }
            lastBy[c.thingIDNumber] = now;
            todayBy[c.thingIDNumber] = (day, TodayBy(c, day) + 1);
            lastOne = c;
            if (lastBy.Count > 300) lastBy.Clear();
            if (todayBy.Count > 300) todayBy.Clear();
        }

        // ================= Small facts =================

        private static int TodayBy(Pawn c, int day) => todayBy.TryGetValue(c.thingIDNumber, out var d) && d.day == day ? d.n : 0;

        /// <summary>News of their town heard from anyone of theirs in the last two days.</summary>
        private static bool HeardLately(OutsiderBook book, Pawn x, int now)
        {
            var town = Outsiders.TownOf(x);
            if (town == null) return false;
            foreach (var h in book.heard)
                if (h != null && h.town == town.ID && h.tick <= now && now - h.tick < 2 * GenDate.TicksPerDay)
                    return true;
            return false;
        }

        private static bool Shy(Pawn c)
        {
            var traits = c.story?.traits?.allTraits;
            if (traits == null) return false;
            foreach (var t in traits)
            {
                string n = t?.def?.defName;
                if (n == null) continue;
                if (n == "Shy" || n == "Recluse" || n == "Loner" || n == "Introvert" || n.EndsWith("_Shy") || n.EndsWith("_Recluse") || n.EndsWith("_Loner") || n.EndsWith("_Introvert"))
                    return true;
            }
            return false;
        }

        /// <summary>One who stays off drink (the game's teetotaler).</summary>
        private static bool Teetotal(Pawn c)
        {
            var traits = c.story?.traits;
            return traits != null && TraitDefOf.DrugDesire != null && traits.HasTrait(TraitDefOf.DrugDesire) && traits.DegreeOfTrait(TraitDefOf.DrugDesire) < 0;
        }

        /// <summary>
        /// What may go as a gift by themselves: nothing the colony's safety rests on (medicine, weapons, clothes) and no
        /// silver or gold; of drugs, only the social ones (a drink, a smoke).
        /// </summary>
        private static bool GiftKind(Thing t)
        {
            var d = t.def;
            if (d.IsMedicine || d.IsWeapon || d.IsApparel || d == ThingDefOf.Silver || d == ThingDefOf.Gold || t is MinifiedThing) return false;
            if (d.IsDrug && d.ingestible?.drugCategory != DrugCategory.Social) return false;
            return true;
        }

        /// <summary>Meals in the colony's stores (not paste made on demand, not raw food).</summary>
        private static int MealsStored(Map map)
        {
            int n = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
            {
                var pref = t?.def?.ingestible?.preferability ?? FoodPreferability.Undefined;
                if (pref < FoodPreferability.MealAwful || pref > FoodPreferability.MealLavish || t.def.IsDrug || !t.Spawned || !t.IsInAnyStorage()) continue;
                n += t.stackCount;
            }
            return n;
        }

        /// <summary>How many of this kind the colony has in its stores.</summary>
        private static int Stored(Map map, ThingDef def)
        {
            int n = 0;
            foreach (var t in map.listerThings.ThingsOfDef(def))
                if (t != null && t.Spawned && t.IsInAnyStorage())
                    n += t.stackCount;
            return n;
        }
    }
}
