using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dealings with a whole people or one of their towns (docs/design/diplomacy-screen.md, step B, points 6 to 13), received
    /// by their council: a supply contract, relief, a proclamation, a crew or guards hired, a petition, a joint work, market
    /// rights, a town invited or the colony's entertainers sent. Each is asked by a letter to the council (the purpose
    /// Dealing, Dealings/DealingLetters.cs), which travels as every letter does (the post, the colony's rider, a relay, a
    /// call: Post/) to the town's head, who answers for the council by their regard and their people's ways
    /// (DealingMath.Chance); the answer comes back the same way, and when it is in the dealing plays out on the map: their
    /// porters at the gate (Couriers/), their traders and visitors (the game's own arrivals), a camp of theirs (Camps/), a
    /// raid kept away, a road on the world map, a warning walked in. Their whole people remember how it went
    /// (World/FactionMemory.cs, goodwill). Run on the neighbours' hour (World/Notables.cs).
    /// </summary>
    public static class Dealings
    {
        /// <summary>The setting "Dealings with a whole people or town" (with the letters on).</summary>
        public static bool On => (ColonyCouncilMod.Settings?.townDealings ?? true) && NeighbourLetters.On;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static DealingBook Book => DealingBook.Instance;

        public static int Version { get; private set; }

        public static void Changed()
        {
            Version++;
            PeoplesModel.Dirty();
            PeopleActs.Dirty();
        }

        private static Map Home => HappeningTracker.HomeMap() ?? Homes.Seat;

        // ================= Words =================

        public static string W(string key, params NamedArgument[] args) => key.Translate(args).Resolve();

        public static string Why(string why) => DealingMath.WhyKey(why).CanTranslate() ? DealingMath.WhyKey(why).Translate().Resolve() : why;

        public static string Label(DealKind k, byte sub) => DealingMath.LabelKey(k, sub).Translate().Resolve();

        /// <summary>"the council of Ashford", "the elders of Ashford" by their age, "the Empire's court at Ashford"; a people as a whole: "the Wheel's leaders".</summary>
        public static string Council(Faction f, Settlement town)
        {
            string age = Ages.IsEmpire(f) ? "Empire" : Ages.Of(f).ToString();
            if (town == null) return W("CC_Deal_Leaders_" + age, (f?.Name ?? "?").Named("PEOPLE"));
            return W("CC_Deal_Council_" + age, town.Name.Named("TOWN"), (f?.Name ?? "?").Named("PEOPLE"));
        }

        // ================= Who receives it =================

        /// <summary>
        /// The one who receives it for their council: the town's head (or whoever of theirs the colony knows there); for the
        /// people as a whole, their head. Null when the colony knows nobody there by name.
        /// </summary>
        public static Notable Receiver(Faction f, Settlement town, Map home)
        {
            var book = NotableBook.Instance;
            if (book == null || f == null) return null;
            if (town != null)
            {
                var at = book.notables.Where(n => n != null && n.Alive && n.faction == f && n.town == town.ID).ToList();
                return at.FirstOrDefault(n => n.role == NotableRole.Head) ?? at.OrderBy(n => (int)n.role).FirstOrDefault();
            }
            return FavourCalls.HeadOf(f, home) ?? book.notables.Where(n => n != null && n.Alive && n.faction == f).OrderBy(n => (int)n.role).FirstOrDefault();
        }

        /// <summary>A letter of a dealing (or a reply) open to their people now, for this topic.</summary>
        public static NeighbourLetter OpenLetter(string topicKey) =>
            NotableBook.Instance?.letters.FirstOrDefault(l => l.Open && l.purpose == LetterPurpose.Dealing && l.topicKey == topicKey);

        // ================= What is true now =================

        private static readonly string[] Wrongs = { "raided", "killedOurs", "camped" };

        /// <summary>The facts a dealing's openness is read from: what Rimfolk tracks of them, the town and the colony.</summary>
        public static DealFacts Facts(DealKind k, byte sub, Faction f, Settlement town, Map home)
        {
            var x = new DealFacts { settingOn = On };
            if (f == null || home == null) return x;
            int now = Now;
            x.hostile = Diplomacy.Hostile(f);
            x.goodwill = f.HasGoodwill ? f.PlayerGoodwill : 0;
            var to = Receiver(f, town, home);
            x.noOne = to == null;
            x.regard = to != null ? NotableBook.Opinion(to) : 0;
            // Out of reach: no way to send it (no post, rider, relay, chain or radio); the colony's rider reaches any town, so only a town cut off by land with nothing else.
            if (town != null && !x.hostile)
            {
                var info = PostReach.Of(home, town, true);
                x.unreachable = info.band == ReachBand.Cut && info.walkDays < 0f && !info.call && info.relay == null;
            }
            string key = DealingMath.TopicKey(k, sub, town?.ID ?? -1);
            var book = Book;
            x.already = OpenLetter(key) != null || book != null && book.deals.Any(d => d.Live && d.people == f && d.kind == k && d.sub == sub && (town == null || d.town == town.ID));
            x.lately = book != null && book.deals.Any(d => d.stage == DealStage.Refused && d.people == f && d.kind == k && d.sub == sub && d.asked >= 0
                                                          && now - d.asked < DealingMath.RefusedDays * DealingMath.DayTicks);
            x.campsOn = CampTracker.Instance != null && (ColonyCouncilMod.Settings?.camps ?? true);
            x.feastsOn = Feasts.On;
            x.missionsOn = true;
            try
            {
                switch (k)
                {
                    case DealKind.Supply:
                        if (sub == (byte)SupplyWay.Ours) x.surplus = Pacts.CartGoods(home).Count > 0;
                        else x.silver = TheirGoods(f, home).Any(o => o.why == null);
                        break;
                    case DealKind.Relief:
                        x.hardTimes = HardTimes(f, town, home, out _);
                        x.relief = ReliefGoods(home).Count > 0;
                        break;
                    case DealKind.Hire:
                        x.campRoom = CampRoom(home);
                        x.crops = sub != (byte)HireKind.Crew || CropsToReap(home);
                        x.silver = Silver(home) >= HireWage(f, home, (HireKind)sub);
                        break;
                    case DealKind.Petition:
                        var mem = FactionMemory.Instance;
                        x.raidedLately = mem != null && mem.deeds.Any(d => d.faction == f && d.kind == "raided" && now - d.tick < 60 * DealingMath.DayTicks);
                        x.wrongedLately = WrongWeight(f) > 0;
                        x.refugeesHere = RefugeeCamp(home) != null;
                        x.hardTimes = HardTimes(f, town, home, out _);
                        x.campRoom = CampRoom(home);
                        break;
                    case DealKind.Work:
                        x.built = town != null && book != null && book.deals.Any(d => d.kind == DealKind.Work && d.sub == sub && d.town == town.ID && (d.Live || d.stage == DealStage.Kept));
                        x.enemies = Find.FactionManager.AllFactionsListForReading.Any(o => o != null && !o.IsPlayer && !o.Hidden && !o.defeated && o.HostileTo(Faction.OfPlayer));
                        x.surplus = WorkShareWhyNot((WorkKind)sub, home, town) == null;
                        break;
                    case DealKind.Market:
                        x.silver = Silver(home) >= DealingMath.MarketFee(Ages.Of(f));
                        break;
                    case DealKind.Invite:
                        // A gathering far enough off for the answer to come back and their folk to set out.
                        x.gathering = GatheringIn(home, out _) >= DealingMath.InviteMinDays;
                        x.guestsOk = !x.hostile;
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: the facts", e);
            }
            return x;
        }

        private static readonly Dictionary<string, (string why, float at, int version)> whyKept = new Dictionary<string, (string, float, int)>();

        /// <summary>Why this dealing can't be asked now, in words, or null (kept for a second: the cards, Peoples and the letter's window ask often).</summary>
        public static string WhyNot(DealKind k, byte sub, Faction f, Settlement town, Map home)
        {
            if (f == null) return Why("NoOne");
            if (DealingMath.TownOnly(k) && town == null) return Why("NoTown");
            string cacheKey = k + ":" + sub + ":" + f.loadID + ":" + (town?.ID ?? -1) + ":" + (home?.uniqueID ?? -1);
            float t = Time.realtimeSinceStartup;
            int open = NeighbourLetters.UnderWay;
            if (whyKept.TryGetValue(cacheKey, out var kept) && t - kept.at < 1f && t >= kept.at && kept.version == Version + open * 100003) return kept.why;
            string why = DealingMath.WhyNot(k, sub, Facts(k, sub, f, town, home));
            string words = why == null ? null : DealingMath.WhyKey(why).Translate((f.Name ?? "?").Named("PEOPLE"), (town?.Name ?? f.Name ?? "?").Named("TOWN")).Resolve();
            if (whyKept.Count > 400) whyKept.Clear();
            whyKept[cacheKey] = (words, t, Version + open * 100003);
            return words;
        }

        /// <summary>The first sub-kind of a dealing that is open now, or the first one's reason.</summary>
        public static byte FirstOpen(DealKind k, Faction f, Settlement town, Map home, out string why)
        {
            why = null;
            string first = null;
            for (byte s = 0; s < DealingMath.Subs(k); s++)
            {
                string w = WhyNot(k, s, f, town, home);
                if (w == null) return s;
                first ??= w;
            }
            why = first;
            return 0;
        }

        // ================= The colony's stores =================

        public static int Silver(Map home) => home == null ? 0 : PlayCarry.Have(home, "silver", ThingDefOf.Silver);

        /// <summary>What the colony could send as relief: its most plentiful medicine, meals and raw food, each in the amount relief carries.</summary>
        public static List<(ThingDef def, int count)> ReliefGoods(Map home)
        {
            var list = new List<(ThingDef, int)>();
            if (home?.resourceCounter == null) return list;
            foreach (var what in DealingMath.ReliefGoods)
            {
                int want = DealingMath.ReliefCount(what);
                var best = home.resourceCounter.AllCountedAmounts.Where(kv => kv.Key != null && kv.Value >= want && Is(kv.Key, what)).OrderByDescending(kv => kv.Value).FirstOrDefault();
                if (best.Key != null) list.Add((best.Key, want));
            }
            return list;
        }

        private static bool Is(ThingDef d, string what)
        {
            switch (what)
            {
                case "medicine": return d.IsMedicine;
                case "meals": return d.ingestible != null && d.ingestible.IsMeal && !d.IsDrug;
                default:
                    // Raw food a cook can use: what the fields and the hunt bring in, not a meal, a drug or a corpse.
                    return d.IsNutritionGivingIngestible && d.ingestible != null && !d.ingestible.IsMeal && !d.IsDrug && !d.IsCorpse && d.category == ThingCategory.Item
                           && (d.ingestible.preferability == FoodPreferability.RawBad || d.ingestible.preferability == FoodPreferability.RawTasty);
            }
        }

        /// <summary>
        /// What they could deliver each season, by their age (food their land grows, wood, cloth; steel and stone in the
        /// medieval age; components and their medicine in the industrial): each with the count a load carries and the silver
        /// the colony pays at the gate, greyed when its stores hold too little silver for it.
        /// </summary>
        public static List<FavourCalls.Option> TheirGoods(Faction f, Map home)
        {
            var list = new List<FavourCalls.Option>();
            var age = Ages.Of(f);
            var names = new List<string> { "RawPotatoes", "RawRice", "WoodLog", "Cloth" };
            if (age >= WorldAge.Medieval)
            {
                names.Add("Steel");
                names.Add("BlocksGranite");
            }
            names.Add(age >= WorldAge.Industrial ? "MedicineIndustrial" : "MedicineHerbal");
            if (age >= WorldAge.Industrial) names.Add("ComponentIndustrial");
            int silver = Silver(home);
            foreach (var n in names)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (def == null) continue;
                int count = DealingMath.TheirLoadCount(def.BaseMarketValue, age);
                int pay = DealingMath.TheirLoadPay(def.BaseMarketValue, count);
                list.Add(new FavourCalls.Option
                {
                    code = DealingLetters.TermsPrefix + def.defName + ":" + count,
                    label = W("CC_Deal_Ask_TheirLoad", Couriers.Amount(count, null, def).Named("GOODS"), pay.Named("SILVER")),
                    why = silver < pay ? W("CC_Deal_Why_PayShort", pay.Named("SILVER")) : null
                });
            }
            return list;
        }

        /// <summary>The colony's share for a joint work: its goods, by the work and the age, and how many, from the colony's tiles to the town.</summary>
        public static (string what, ThingDef def, int count) WorkShare(WorkKind w, Map home, Settlement town)
        {
            float tiles = home != null && town != null ? LivingEvents.Distance(home.Tile, town.Tile) : 10f;
            string what = DealingMath.ShareGoods(w, Ages.Ours);
            int count = DealingMath.WorkShare(w, tiles);
            ThingDef def;
            if (what == "blocks")
            {
                def = home?.resourceCounter?.AllCountedAmounts.Where(kv => kv.Key != null && kv.Key.IsWithinCategory(ThingCategoryDefOf.StoneBlocks) && kv.Value >= count)
                    .OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault() ?? DefDatabase<ThingDef>.GetNamedSilentFail("BlocksGranite");
            }
            else def = what == "steel" ? ThingDefOf.Steel : ThingDefOf.WoodLog;
            return (def?.defName ?? what, def, count);
        }

        public static string WorkShareWhyNot(WorkKind w, Map home, Settlement town)
        {
            var (what, def, count) = WorkShare(w, home, town);
            if (def == null || home == null) return Why("NoShare");
            return PlayCarry.Have(home, def.defName, def) >= count ? null : W("CC_Deal_Why_ShareShort", Couriers.Amount(count, null, def).Named("GOODS"));
        }

        /// <summary>The silver a crew or guards would ask every five days, as the camps' contract counts it (by their number and age).</summary>
        public static int HireWage(Faction f, Map home, HireKind k) => PeoplesEMath.Wage(k == HireKind.Guard ? 4 : 5, Peoples.AgeOf(f));

        // ================= Hard times, wrongs, refugees, the land =================

        /// <summary>
        /// The town in hard times the colony has heard of: struggling by its fortune (World/Region/), or a world event that
        /// falls hard on their people (a famine, a plague, a fire: World/WorldSituations.cs). <paramref name="what"/>: its
        /// name in words.
        /// </summary>
        public static bool HardTimes(Faction f, Settlement town, Map home, out string what)
        {
            what = null;
            if (f == null) return false;
            var ws = WorldSituations.Instance;
            var s = ws?.Running?.FirstOrDefault(x => x?.def != null && x.def.bad && x.ctx?.faction == f);
            if (s != null)
            {
                what = !(s.ctx?.name).NullOrEmpty() ? s.ctx.name : Find.ActiveLanguageWorker.WithIndefiniteArticle(s.def.label ?? "?");
                return true;
            }
            if (town != null && RegionFortunes.Of(town.ID) == Fortune.Struggling)
            {
                var rec = WorldLedger.TownRecord(town.ID);
                what = RegionFortunes.Line(rec) ?? RegionFortunes.Word(Fortune.Struggling, f);
                return true;
            }
            return false;
        }

        /// <summary>The weight of their wrongs against the colony in the last 60 days (raids, the colony's dead, a raiders' camp), 0 for none.</summary>
        public static int WrongWeight(Faction f)
        {
            var mem = FactionMemory.Instance;
            if (mem == null || f == null) return 0;
            int now = Now;
            return mem.deeds.Where(d => d.faction == f && !d.settled && Wrongs.Contains(d.kind) && now - d.tick < 60 * DealingMath.DayTicks).Sum(d => d.weight);
        }

        /// <summary>A refugee family camped at the colony (the camps' refugee family), or null.</summary>
        public static Camp RefugeeCamp(Map home)
        {
            var t = CampTracker.Instance;
            if (t == null || home == null) return null;
            return t.All.FirstOrDefault(c => c != null && c.map == home && c.kind?.defName == RiseFallEffects.RefugeeCamp && c.phase != CampPhase.Leaving && c.phase != CampPhase.Over);
        }

        private static readonly Dictionary<int, (bool room, float at)> campRoom = new Dictionary<int, (bool, float)>();

        /// <summary>A camp could be pitched at the colony now: a spot for it (looked for every ten seconds a map), and nothing coming at the colony.</summary>
        public static bool CampRoom(Map home)
        {
            if (home == null || MapFacts.ThreatOn(home)) return false;
            float t = Time.realtimeSinceStartup;
            if (campRoom.TryGetValue(home.uniqueID, out var kept) && t - kept.at < 10f && t >= kept.at) return kept.room;
            bool room = CampSite.TryFind(home, out _);
            campRoom[home.uniqueID] = (room, t);
            return room;
        }

        /// <summary>The colony's fields have crops growing or ripe to reap.</summary>
        public static bool CropsToReap(Map home)
        {
            if (home == null) return false;
            foreach (var z in home.zoneManager.AllZones.OfType<Zone_Growing>())
                foreach (var t in z.AllContainedThings)
                    if (t is Plant p && p.def.plant != null && p.def.plant.Harvestable && p.Growth > 0.4f)
                        return true;
            return false;
        }

        /// <summary>
        /// Days until a gathering the colony holds: its feast with guests set for a day, a fair it proclaimed, or a day of
        /// its calendar kept with a gathering within two weeks; -1 for none. <paramref name="what"/>: its name.
        /// </summary>
        public static int GatheringIn(Map home, out string what)
        {
            what = null;
            if (home == null) return -1;
            int best = -1;
            try
            {
                var plan = Feasts.On ? Feasts.At(home) : null;
                if (plan != null && plan.Live && !plan.calledOff && plan.day >= 0)
                {
                    int d = plan.day - Feasts.Today(home);
                    if (d >= 0)
                    {
                        best = d;
                        what = W("CC_Deal_Gathering_Feast");
                    }
                }
                var fair = Book?.deals.FirstOrDefault(x => x.Live && x.kind == DealKind.Proclaim && x.sub == (byte)ProclaimKind.Fair && x.home == home && x.next >= 0);
                if (fair != null)
                {
                    int d = Mathf.Max(0, (fair.next - Now) / DealingMath.DayTicks);
                    if (best < 0 || d < best)
                    {
                        best = d;
                        what = W("CC_Deal_Gathering_Fair");
                    }
                }
                var cal = ColonyCalendar.On ? ColonyCalendar.For(home) : null;
                if (cal != null)
                {
                    int today = GenLocalDate.DayOfYear(home);
                    foreach (var day in cal.days)
                    {
                        if (day == null || !day.Dated || !Gathers(day.way)) continue;
                        int until = CalendarMath.DaysUntil(today, day.day);
                        if (until < 1 || until > DealingMath.FairMaxDays) continue;
                        if (best < 0 || until < best)
                        {
                            best = until;
                            what = day.label ?? ColonyCalendar.DefaultLabel(day);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: the gatherings", e);
            }
            return best;
        }

        private static bool Gathers(DayWay w) => w == DayWay.Feast || w == DayWay.Games || w == DayWay.Bonfire || w == DayWay.Gifts || w == DayWay.Custom;

        /// <summary>The calendar's next gathering day within the fair's window, in days, or -1.</summary>
        public static int CalendarGatheringIn(Map home)
        {
            var cal = ColonyCalendar.On && home != null ? ColonyCalendar.For(home) : null;
            if (cal == null) return -1;
            int today = GenLocalDate.DayOfYear(home), best = -1;
            foreach (var day in cal.days)
            {
                if (day == null || !day.Dated || !Gathers(day.way)) continue;
                int until = CalendarMath.DaysUntil(today, day.day);
                if (until >= DealingMath.FairMinDays && until <= DealingMath.FairMaxDays && (best < 0 || until < best)) best = until;
            }
            return best;
        }

        // ================= Their answer in, and what follows =================

        /// <summary>A dealing begun: its answer is in (they agreed), and what it brings starts on the map.</summary>
        public static void Begin(Dealing d)
        {
            if (d == null || d.stage != DealStage.Agreed) return;
            int now = Now;
            var home = d.home ?? Home;
            d.home = home;
            d.stage = DealStage.Running;
            d.since = now;
            try
            {
                switch (d.kind)
                {
                    case DealKind.Supply:
                        d.next = now + (int)(DealingMath.FirstDeliveryDays * DealingMath.DayTicks);
                        break;
                    case DealKind.Relief:
                    case DealKind.Work:
                    case DealKind.Market:
                        Collect(d, home);
                        break;
                    case DealKind.Proclaim:
                        BeginProclaim(d, home, now);
                        break;
                    case DealKind.Hire:
                        BeginHire(d, home);
                        break;
                    case DealKind.Petition:
                        BeginPetition(d, home, now);
                        break;
                    case DealKind.Invite:
                        BeginInvite(d, home, now);
                        break;
                    case DealKind.Reply:
                        WorldWrites.Begin(d, home);
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: begun", e);
                End(d, DealStage.Ended);
            }
            Changed();
        }

        /// <summary>Closed: kept, broken or ended; what their people remember of it, and the goodwill.</summary>
        public static void End(Dealing d, DealStage how, string line = null)
        {
            if (d == null || !d.Live && d.stage != DealStage.Running) return;
            d.stage = how;
            d.until = Now;
            var f = d.people;
            try
            {
                string deed = DealingMath.Deed(d.kind, d.sub, how);
                if (deed != null && f != null) FactionMemory.Note(f, deed, d.townName ?? f.Name, deed == "builtTogether" ? DealingMath.WorkWeight : 0);
                // A contract's goodwill at its end (a hired camp's own contract moves their goodwill itself: Camps/CampContract.cs).
                if (f != null && f.HasGoodwill && !f.IsPlayer && !f.defeated && d.kind == DealKind.Supply)
                {
                    if (how == DealStage.Broken) WorldSituationWorker.Goodwill(f, DealingMath.BrokenGoodwill);
                    else if (how == DealStage.Kept) WorldSituationWorker.Goodwill(f, DealingMath.KeptGoodwill);
                }
                if (!line.NullOrEmpty())
                    Tell.Letter(NewsKind.World, Importance.Notable, d.home, W("CC_Deal_EndLabel_" + how, Label(d.kind, d.sub).Named("DEAL"), (d.townName ?? f?.Name ?? "?").Named("TOWN")), line,
                        how == DealStage.Broken ? LetterDefOf.NegativeEvent : how == DealStage.Kept ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent, LookTargets.Invalid, f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: ended", e);
            }
            Changed();
        }

        // ---------------- Their porters at the gate: relief, a work's share, the stall's fee ----------------

        /// <summary>Their porters come to the gate for what the colony sends (Couriers.CollectOrder); without the couriers, it is taken now.</summary>
        private static void Collect(Dealing d, Map home)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(d.goods ?? "");
            if (def == null || d.count <= 0 || home == null)
            {
                End(d, DealStage.Ended);
                return;
            }
            var trip = Couriers.CollectOrder(home, def, d.count, 0, d.people);
            if (trip != null)
            {
                d.tripId = trip.id;
                // apart: what the colony sends is to be fetched; their porters are on their way.
                Notify.Message(W("CC_Deal_Msg_Porters", Council(d.people, d.Town).Named("COUNCIL"), Couriers.Amount(d.count, null, def).Named("GOODS")), LookTargets.Invalid,
                    MessageTypeDefOf.NeutralEvent, Importance.Notable);
                return;
            }
            // Nobody to send, or the goods-on-the-map setting off: taken from the stores now, as before.
            if (!VarietyUtil.TakeFromStores(home, def, d.count))
            {
                Short(d, home, def);
                return;
            }
            Collected(d, home);
        }

        private static void Short(Dealing d, Map home, ThingDef def)
        {
            End(d, DealStage.Ended, W("CC_Deal_Short", Couriers.Amount(d.count, null, def).Named("GOODS"), Council(d.people, d.Town).Named("COUNCIL")));
        }

        /// <summary>What the colony sent is with them: relief eases their town, a work begins, the stall opens.</summary>
        private static void Collected(Dealing d, Map home)
        {
            int now = Now;
            d.tripId = -1;
            var f = d.people;
            switch (d.kind)
            {
                case DealKind.Relief:
                    if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, DealingMath.ReliefGoodwill);
                    RegionFortunes.Move(f, d.town, DealingMath.ReliefFortune, W("CC_Deal_Cause_Relief"));
                    End(d, DealStage.Kept, W("CC_Deal_Relief_Kept", Council(f, d.Town).Named("COUNCIL"), (d.townName ?? "?").Named("TOWN"), (f?.Name ?? "?").Named("PEOPLE")));
                    break;
                case DealKind.Work:
                    d.done = 1;
                    float tiles = home != null && d.Town != null ? LivingEvents.Distance(home.Tile, d.Town.Tile) : 10f;
                    d.next = now + (int)(DealingMath.WorkDays((WorkKind)d.sub, tiles) * DealingMath.DayTicks);
                    Tell.Message(NewsKind.World, Importance.Notable, home, W("CC_Deal_Work_Begun_" + (WorkKind)d.sub, (d.townName ?? "?").Named("TOWN"), (f?.Name ?? "?").Named("PEOPLE")),
                        LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                    break;
                case DealKind.Reply:
                    WorldWrites.Granted(d);
                    return;
                case DealKind.Market:
                    d.done = 1;
                    d.until = now + (int)(DealingMath.MarketDays * DealingMath.DayTicks);
                    d.next = now + (int)(DealingMath.MarketCaravanGapDays * DealingMath.DayTicks);
                    Tell.Letter(NewsKind.World, Importance.Notable, home, W("CC_Deal_Market_OpenLabel", (d.townName ?? "?").Named("TOWN")),
                        W("CC_Deal_Market_Open", (d.townName ?? "?").Named("TOWN"), (f?.Name ?? "?").Named("PEOPLE"), DealingMath.MarketOffset.ToStringPercent().Named("TERMS")),
                        LetterDefOf.PositiveEvent, LookTargets.Invalid, f);
                    break;
            }
            Changed();
        }

        // ---------------- Proclamations ----------------

        private static void BeginProclaim(Dealing d, Map home, int now)
        {
            var f = d.people;
            switch ((ProclaimKind)d.sub)
            {
                case ProclaimKind.Fair:
                    // The fair's morning: the gathering day on the colony's calendar within the window, else a few days on.
                    int days = DealingMath.FairIn(CalendarGatheringIn(home));
                    d.next = Feasts.DayStart(home, Feasts.Today(home) + days) + DealingMath.FairHourFrom * GenDate.TicksPerHour;
                    break;
                case ProclaimKind.Friendship:
                    if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, DealingMath.FriendshipGoodwill2);
                    var n = NotableBook.Instance?.Get(d.notable);
                    if (n != null) n.personal += DealingMath.FriendshipRegard;
                    End(d, DealStage.Kept);
                    break;
                default:
                    d.until = now + (int)(DealingMath.SeasonDays * DealingMath.DayTicks);
                    d.next = now + (int)(Rand.Range(DealingMath.TermsCaravanMin, DealingMath.TermsCaravanMax) * DealingMath.DayTicks);
                    break;
            }
        }

        /// <summary>The game's own arrival of their people: a trader caravan, or visitors. True when it came.</summary>
        public static bool Arrival(IncidentDef inc, Map home, Faction f)
        {
            if (inc == null || home == null || f == null || Diplomacy.Hostile(f)) return false;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(inc.category, home);
                parms.faction = f;
                parms.forced = true;
                return inc.Worker.CanFireNow(parms) && inc.Worker.TryExecute(parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: their people come", e);
                return false;
            }
        }

        // ---------------- A crew or guards hired ----------------

        public const string GuardCamp = "Rimfolk_Camp_HiredGuard", CrewCamp = "Rimfolk_Camp_HarvestHands";

        private static void BeginHire(Dealing d, Map home)
        {
            var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(d.sub == (byte)HireKind.Guard ? GuardCamp : CrewCamp);
            var camp = kind != null ? StartCamp(home, kind, d.people, DealingMath.SeasonDays) : null;
            if (camp == null)
            {
                End(d, DealStage.Ended, W("CC_Deal_Hire_NoCamp", Council(d.people, d.Town).Named("COUNCIL")));
                return;
            }
            d.campId = camp.id;
            if (d.sub == (byte)HireKind.Crew)
            {
                // Taken on already by the letter: paid in silver when they ask, as the harvest hands are.
                camp.reapPay = "silver";
                camp.offerMade = true;
            }
            CampTracker.AnnounceCamp(camp);
        }

        /// <summary>
        /// A camp of theirs the colony asked for (hired, or refugees taken in): pitched like any camp, walking in from the edge
        /// nearest its spot, for so many days; beside the camps already there (a hired camp isn't a wandering one). Null
        /// when none can be pitched now.
        /// </summary>
        public static Camp StartCamp(Map home, CampKindDef kind, Faction f, float days)
        {
            var tracker = CampTracker.Instance;
            if (tracker == null || home == null || kind == null || f == null || Diplomacy.Hostile(f)) return null;
            try
            {
                var usual = CampWeather.StayRange(home, (int)(days * GenDate.TicksPerDay));
                bool hot = CampWeatherMath.PitchForHeat(usual.high);
                if (!CampSite.TryFind(home, out var center, hot)) return null;
                center = CampLand.SiteFor(kind, home, center, hot);
                if (!center.IsValid) return null;
                var setup = tracker.Make(home, center, CampSite.ColonyCenter(home), kind, f, hot, null, null);
                if (setup == null) return null;
                setup.stayTicks = (int)(days * GenDate.TicksPerDay);
                return tracker.Start(setup, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: a camp of theirs", e);
                return null;
            }
        }

        // ---------------- Petitions ----------------

        private static void BeginPetition(Dealing d, Map home, int now)
        {
            var f = d.people;
            switch ((PetitionKind)d.sub)
            {
                case PetitionKind.Raiders:
                    // Their raiders kept away for a term, as a truce does (Dealings/DealingPatches.cs).
                    d.until = now + (int)(DealingMath.RaidersDays * DealingMath.DayTicks);
                    break;
                case PetitionKind.Reparations:
                    // The silver came with their answer, set down at the gate by their messenger.
                    foreach (var deed in FactionMemory.Instance?.deeds.Where(x => x.faction == f && !x.settled && Wrongs.Contains(x.kind)).ToList() ?? new List<FactionDeed>())
                        FactionMemory.Settle(deed);
                    End(d, DealStage.Kept);
                    break;
                case PetitionKind.Settle:
                    var camp = RefugeeCamp(home);
                    if (camp == null)
                    {
                        End(d, DealStage.Ended);
                        return;
                    }
                    // The refugees go to their town in their own time: they pack and walk off the map.
                    var theirs = camp.faction;
                    camp.Leave("moved");
                    if (theirs != null && theirs != f) FactionMemory.Note(theirs, "sheltered", d.townName);
                    End(d, DealStage.Kept);
                    break;
                default:
                    var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(RiseFallEffects.RefugeeCamp);
                    var c = kind != null ? StartCamp(home, kind, f, kind.days.RandomInRange) : null;
                    if (c == null)
                    {
                        End(d, DealStage.Ended, W("CC_Deal_TakeIn_NoCamp", Council(f, d.Town).Named("COUNCIL")));
                        return;
                    }
                    d.campId = c.id;
                    CampTracker.AnnounceCamp(c);
                    End(d, DealStage.Kept);
                    break;
            }
        }

        /// <summary>Raids of this people are kept away from the colony's homes by a petition they granted, now.</summary>
        public static bool KeepsAway(Faction f)
        {
            var book = Book;
            if (book == null || f == null) return false;
            int now = Now;
            foreach (var d in book.deals)
                if (d.stage == DealStage.Running && d.kind == DealKind.Petition && d.sub == (byte)PetitionKind.Raiders && d.people == f && d.until > now)
                    return true;
            return false;
        }

        /// <summary>A raid of theirs came all the same (one their people sent beforehand, a quest's, another mod's): their word is broken.</summary>
        public static void Raided(Faction f, Map map)
        {
            var book = Book;
            if (book == null || f == null) return;
            foreach (var d in book.deals.Where(x => x.stage == DealStage.Running && x.kind == DealKind.Petition && x.sub == (byte)PetitionKind.Raiders && x.people == f).ToList())
                End(d, DealStage.Broken, W("CC_Deal_Raiders_Broken", (f.Name ?? "?").Named("PEOPLE")));
        }

        // ---------------- A town invited, the colony's entertainers ----------------

        private static void BeginInvite(Dealing d, Map home, int now)
        {
            if (d.sub == (byte)InviteKind.Entertainers)
            {
                if (!DealingFestival.Invite(d, home))
                {
                    End(d, DealStage.Ended, W("CC_Deal_Festival_NoMission", (d.townName ?? "?").Named("TOWN")));
                    return;
                }
                End(d, DealStage.Kept);
                return;
            }
            int days = GatheringIn(home, out _);
            if (days < 1)
            {
                End(d, DealStage.Ended, W("CC_Deal_Invite_NoGathering", Council(d.people, d.Town).Named("COUNCIL")));
                return;
            }
            d.next = Feasts.DayStart(home, Feasts.Today(home) + days) + DealingMath.FairHourFrom * GenDate.TicksPerHour;
        }

        // ================= The hour =================

        public static void Hourly(int now)
        {
            var book = Book;
            if (book == null) return;
            if (Sched.FirstHourOfDay(now)) book.Tidy(now);
            if (book.deals.Count > 0)
                foreach (var d in book.deals.ToList())
                {
                    // Read and answered, but the answer never counted in (a letter let go on its way back): settled as it stands.
                    if (d.stage == DealStage.Asked) Unanswered(d);
                    if (d.stage != DealStage.Running) continue;
                    try
                    {
                        Step(d, now);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("dealings: " + d.kind, e);
                        End(d, DealStage.Ended);
                    }
                }
            // Pen friends and the world's letters (Dealings/PenFriends.cs, WorldWrites.cs).
            if (Sched.FirstHourOfDay(now))
            {
                PenFriends.Daily(now);
                WorldWrites.Daily(now);
            }
            WorldWrites.Hourly(now);
        }

        /// <summary>A dealing whose letter is done or gone without its answer counted in: an answer that came is counted now; a letter called off ends it.</summary>
        private static void Unanswered(Dealing d)
        {
            var l = NeighbourLetters.Get(d.letter);
            if (l != null && l.Open) return;
            if (l != null && l.stage == LetterStage.Cancelled)
            {
                End(d, DealStage.Ended);
                return;
            }
            if (l != null) DealingLetters.AnswerIn(l);
            else if (d.yes)
            {
                d.stage = DealStage.Agreed;
                Begin(d);
            }
            else
            {
                d.stage = DealStage.Refused;
                Changed();
            }
        }

        private static void Step(Dealing d, int now)
        {
            var home = d.home;
            var f = d.people;
            if (home == null || !Find.Maps.Contains(home))
            {
                End(d, DealStage.Ended);
                return;
            }
            // Their people at war with the colony, gone, or the town fallen: what was agreed is over, nobody's fault but the war's.
            bool petition = d.kind == DealKind.Petition;
            if (f == null || f.defeated || !petition && Diplomacy.Hostile(f) || DealingMath.TownOnly(d.kind) && d.town >= 0 && d.Town == null && d.kind != DealKind.Work)
            {
                End(d, DealStage.Ended, W("CC_Deal_Overtaken", Label(d.kind, d.sub).Named("DEAL"), (d.townName ?? f?.Name ?? "?").Named("TOWN")));
                return;
            }
            // A courier trip under way: when it settles, what it carried is with them.
            if (d.tripId >= 0)
            {
                var trip = CourierTracker.Instance?.Get(d.tripId);
                if (trip != null && !trip.settled) return;
                if (d.kind == DealKind.Supply)
                {
                    d.tripId = -1;
                    return;
                }
                Collected(d, home);
                return;
            }
            switch (d.kind)
            {
                case DealKind.Supply:
                    SupplyHour(d, home, now);
                    break;
                case DealKind.Proclaim:
                    if (d.sub == (byte)ProclaimKind.Fair) FairHour(d, home, now, false);
                    else if (d.sub == (byte)ProclaimKind.Terms) TermsHour(d, home, now);
                    break;
                case DealKind.Invite:
                    FairHour(d, home, now, true);
                    break;
                case DealKind.Hire:
                    var camp = CampTracker.Instance?.ById(d.campId);
                    if (camp == null || camp.phase == CampPhase.Over || camp.phase == CampPhase.Leaving)
                    {
                        // Gone at their season's end paid, or walked off unpaid (the camp's own letter says which).
                        bool unpaid = camp != null && camp.ending == "unpaid";
                        End(d, unpaid ? DealStage.Broken : DealStage.Kept);
                    }
                    break;
                case DealKind.Petition:
                    if (d.until >= 0 && now >= d.until)
                        End(d, DealStage.Kept, W("CC_Deal_Raiders_Over", (f.Name ?? "?").Named("PEOPLE")));
                    break;
                case DealKind.Work:
                    if (d.done == 1 && d.next >= 0 && now >= d.next) Built(d, home);
                    break;
                case DealKind.Market:
                    MarketHour(d, home, now);
                    break;
            }
        }

        // ---------------- A supply contract's season ----------------

        private static void SupplyHour(Dealing d, Map home, int now)
        {
            if (d.next < 0 || now < d.next || MapFacts.ThreatOn(home)) return;
            int hour = GenLocalDate.HourOfDay(home);
            if (hour < 8 || hour > 15) return;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(d.goods ?? "");
            var f = d.people;
            if (def == null)
            {
                End(d, DealStage.Ended);
                return;
            }
            bool ours = d.sub == (byte)SupplyWay.Ours;
            bool kept;
            if (ours)
            {
                // Their porters come with the silver and take the goods from the stores (Couriers); short, the season is missed.
                kept = PlayCarry.Have(home, def.defName, def) >= d.count;
                if (kept)
                {
                    var trip = Couriers.CollectOrder(home, def, d.count, d.silver, f);
                    if (trip != null) d.tripId = trip.id;
                    else kept = Couriers.Sell(home, def, d.count, d.silver, f, DropCellFinder.TradeDropSpot(home));
                }
            }
            else
            {
                // Their porters bring the goods to the gate and take the colony's silver; without it, they turn back.
                kept = Silver(home) >= d.silver;
                if (kept)
                {
                    var goods = Couriers.Make(def, d.count);
                    if (!Couriers.Exchange(home, "silver", ThingDefOf.Silver, d.silver, goods, f))
                    {
                        kept = VarietyUtil.TakeFromStores(home, ThingDefOf.Silver, d.silver);
                        if (kept) StorytellerEvents.Hand(def, d.count, home, DropCellFinder.TradeDropSpot(home));
                    }
                }
            }
            d.next = now + (int)(DealingMath.SeasonDays * DealingMath.DayTicks);
            string goodsWord = Couriers.Amount(d.count, null, def);
            if (kept)
            {
                d.done++;
                d.missedRow = 0;
                if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, DealingMath.DeliveryGoodwill, false);
                Tell.Message(NewsKind.World, Importance.Notable, home, W(ours ? "CC_Deal_Supply_OursDue" : "CC_Deal_Supply_TheirsDue", Council(f, d.Town).Named("COUNCIL"),
                    goodsWord.Named("GOODS"), d.silver.Named("SILVER")), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                d.missed++;
                d.missedRow++;
                if (DealingMath.Broken(d.missedRow, d.missed))
                {
                    End(d, DealStage.Broken, W("CC_Deal_Supply_Broken", Council(f, d.Town).Named("COUNCIL"), (f.Name ?? "?").Named("PEOPLE")));
                    return;
                }
                Tell.Letter(NewsKind.World, Importance.Notable, home, W("CC_Deal_Supply_MissedLabel", (d.townName ?? "?").Named("TOWN")),
                    W(ours ? "CC_Deal_Supply_MissedOurs" : "CC_Deal_Supply_MissedTheirs", Council(f, d.Town).Named("COUNCIL"), goodsWord.Named("GOODS"), d.silver.Named("SILVER")),
                    LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
            }
            if (d.done + d.missed >= DealingMath.Deliveries)
                End(d, d.missed == 0 ? DealStage.Kept : DealStage.Ended, W(d.missed == 0 ? "CC_Deal_Supply_Kept" : "CC_Deal_Supply_Over", Council(f, d.Town).Named("COUNCIL"),
                    (f.Name ?? "?").Named("PEOPLE")));
            Changed();
        }

        // ---------------- A fair day, a town at the colony's feast ----------------

        private static void FairHour(Dealing d, Map home, int now, bool invite)
        {
            if (d.next < 0 || now < d.next) return;
            var f = d.people;
            int hour = GenLocalDate.HourOfDay(home);
            // The morning of the day: a threat on the map puts it off an hour at a time; past noon, it was missed.
            if (MapFacts.ThreatOn(home) && hour < DealingMath.FairHourTo) return;
            if (hour > DealingMath.FairHourTo || now - d.next > GenDate.TicksPerDay)
            {
                End(d, DealStage.Ended, W(invite ? "CC_Deal_Invite_Missed" : "CC_Deal_Fair_Missed", Council(f, d.Town).Named("COUNCIL")));
                return;
            }
            // Their trader caravan by the game's own arrival; their folk walk in from the edge at an errand's pace and stay the day.
            bool traders = !invite && Arrival(IncidentDefOf.TraderCaravanArrival, home, f);
            var folk = Guests(home, f, Rand.RangeInclusive(DealingMath.GuestsMin, DealingMath.GuestsMax), DealingMath.GuestHours, invite ? ArrivalWhy.TownInvited : ArrivalWhy.TownFair);
            d.came = traders || folk != null;
            if (!d.came)
            {
                End(d, DealStage.Ended, W(invite ? "CC_Deal_Invite_NoneCame" : "CC_Deal_Fair_NoneCame", Council(f, d.Town).Named("COUNCIL")));
                return;
            }
            if (folk != null)
                Tell.Message(NewsKind.World, Importance.Notable, home, W(invite ? "CC_Deal_Invite_Came" : "CC_Deal_Fair_Came", (d.townName ?? f.Name ?? "?").Named("TOWN")),
                    new LookTargets(folk), MessageTypeDefOf.PositiveEvent);
            End(d, DealStage.Kept);
        }

        /// <summary>
        /// Folk of theirs come to the colony for the day: they walk in from the edge at an errand's pace and spend the hours
        /// just outside the colony, the game's own visit (LordJob_VisitOnErrand), or with Hospitality as its guests, invited as
        /// they are (<paramref name="why"/>: Integrations/InvitedGuests.cs); the first of them, or null when none could come.
        /// </summary>
        public static Pawn Guests(Map home, Faction f, int count, float hours, ArrivalWhy why)
        {
            var kind = Arrivals.PersonKind(f);
            if (home == null || kind == null || Diplomacy.Hostile(f)) return null;
            var pawns = new List<Pawn>();
            try
            {
                for (int i = 0; i < count; i++)
                {
                    var p = Arrivals.Make(kind, f);
                    if (p != null) pawns.Add(p);
                }
                if (pawns.Count > 0 && Arrivals.Invited(why, pawns, home, f, null, hours) != null) return pawns[0];
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: their folk come", e);
            }
            foreach (var p in pawns.Where(p => !p.Spawned)) Arrivals.Discard(p);
            return null;
        }

        // ---------------- The trade terms posted ----------------

        private static void TermsHour(Dealing d, Map home, int now)
        {
            if (d.until >= 0 && now >= d.until)
            {
                End(d, DealStage.Kept);
                return;
            }
            if (d.came || d.next < 0 || now < d.next || MapFacts.ThreatOn(home)) return;
            int hour = GenLocalDate.HourOfDay(home);
            if (hour < 8 || hour > 15) return;
            d.came = Arrival(IncidentDefOf.TraderCaravanArrival, home, d.people);
            if (!d.came) d.next = now + GenDate.TicksPerDay;
        }

        /// <summary>The better terms the colony has with this people's traders from its dealings: market rights, terms posted, a pen friend.</summary>
        public static float Terms(Faction f)
        {
            var book = Book;
            if (book == null || f == null || f.IsPlayer || !On) return 0f;
            int now = Now;
            bool market = false, terms = false;
            foreach (var d in book.deals)
            {
                if (d.stage != DealStage.Running || d.people != f) continue;
                if (d.kind == DealKind.Market && d.done == 1 && d.until > now) market = true;
                if (d.kind == DealKind.Proclaim && d.sub == (byte)ProclaimKind.Terms && d.until > now) terms = true;
            }
            return DealingMath.Offset(market, terms, PenFriends.AmongThem(f));
        }

        // ---------------- Market rights ----------------

        private static void MarketHour(Dealing d, Map home, int now)
        {
            if (d.done != 1) return;
            if (d.until >= 0 && now >= d.until)
            {
                End(d, DealStage.Kept, W("CC_Deal_Market_Over", (d.townName ?? "?").Named("TOWN"), (d.people?.Name ?? "?").Named("PEOPLE")));
                return;
            }
            if (!Sched.FirstHourOfDay(now) || d.next < 0 || now < d.next || MapFacts.ThreatOn(home)) return;
            if (!Rand.Chance(DealingMath.MarketCaravanChance)) return;
            if (Arrival(IncidentDefOf.TraderCaravanArrival, home, d.people)) d.next = now + (int)(DealingMath.MarketCaravanGapDays * DealingMath.DayTicks);
        }

        // ---------------- A joint work done ----------------

        private static void Built(Dealing d, Map home)
        {
            var f = d.people;
            var town = d.Town;
            d.done = 2;
            var w = (WorkKind)d.sub;
            if (w == WorkKind.Road && town != null) Road(home, town);
            if (w == WorkKind.Well || w == WorkKind.Bridge)
            {
                if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, DealingMath.WorkGoodwill);
                RegionFortunes.Move(f, d.town, DealingMath.WorkFortune, W("CC_Deal_Cause_" + w));
            }
            End(d, DealStage.Kept, W("CC_Deal_Work_Built_" + w, (d.townName ?? "?").Named("TOWN"), (f?.Name ?? "?").Named("PEOPLE")));
        }

        /// <summary>
        /// The road between: the game's own road laid along the world's path from the colony to the town (the kind by the
        /// shared age: a dirt path, a dirt road, a stone road), so caravans and the post go faster that way by the game's own
        /// rules. The world map redrawn, the post's days worked out again.
        /// </summary>
        public static bool Road(Map home, Settlement town)
        {
            if (home == null || town == null || !home.Tile.Valid || !town.Tile.Valid || home.Tile.Layer != town.Tile.Layer) return false;
            var def = DefDatabase<RoadDef>.GetNamedSilentFail(DealingMath.RoadDef(DealingMath.SharedAge(Ages.Ours, Ages.Of(town.Faction))));
            if (def == null) return false;
            try
            {
                var path = home.Tile.Layer.Pather.FindPath(home.Tile, town.Tile, null);
                try
                {
                    if (path == null || !path.Found) return false;
                    var nodes = path.NodesReversed;
                    for (int i = 0; i + 1 < nodes.Count; i++)
                    {
                        var have = Find.WorldGrid.GetRoadDef(nodes[i], nodes[i + 1], false);
                        if (have == null || have.priority < def.priority) Find.WorldGrid.OverlayRoad(nodes[i], nodes[i + 1], def);
                    }
                }
                finally
                {
                    path?.Dispose();
                }
                Find.World.renderer.SetDirty<WorldDrawLayer_Roads>(home.Tile.Layer);
                PostReach.Reset();
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: the road between", e);
                return false;
            }
        }

        /// <summary>A watchtower standing toward one of their towns (built together, the town still theirs, its people at peace).</summary>
        public static List<Settlement> Towers()
        {
            var list = new List<Settlement>();
            var book = Book;
            if (book == null) return list;
            foreach (var d in book.deals)
            {
                if (d.kind != DealKind.Work || d.sub != (byte)WorkKind.Tower || d.stage != DealStage.Kept) continue;
                var town = d.Town;
                if (town != null && town.Faction == d.people && !Diplomacy.Hostile(d.people)) list.Add(town);
            }
            return list;
        }

        // ================= The Dealings tab and the town's card =================

        /// <summary>The colony's dealings with a people, live first, then the closed ones the book still keeps.</summary>
        public static List<Dealing> With(Faction f)
        {
            var book = Book;
            if (book == null || f == null) return new List<Dealing>();
            return book.deals.Where(d => d.people == f && d.kind != DealKind.Reply && (d.Live || d.stage == DealStage.Kept && d.kind == DealKind.Work || Now - (d.until >= 0 ? d.until : d.asked) < 30 * DealingMath.DayTicks))
                .OrderBy(d => d.Live ? 0 : 1).ThenByDescending(d => d.asked).ToList();
        }

        public static bool AnyLive(Faction f) => Book?.deals.Any(d => d.people == f && d.Live && d.kind != DealKind.Reply) ?? false;

        /// <summary>A dealing in a line: what, with whom, and where it stands (never a time left).</summary>
        public static string Line(Dealing d)
        {
            string what = Label(d.kind, d.sub);
            string with = d.town >= 0 ? d.townName ?? "?" : d.people?.Name ?? "?";
            string terms = "";
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(d.goods ?? "");
            if (d.kind == DealKind.Supply && def != null) terms = " " + W("CC_Deal_Line_Supply", Couriers.Amount(d.count, null, def).Named("GOODS"), d.silver.Named("SILVER"));
            string stand;
            switch (d.stage)
            {
                case DealStage.Asked: stand = W("CC_Deal_Stand_Asked"); break;
                case DealStage.Agreed: stand = W("CC_Deal_Stand_Agreed"); break;
                case DealStage.Refused: stand = W("CC_Deal_Stand_Refused"); break;
                case DealStage.Running:
                    stand = d.kind == DealKind.Supply ? W("CC_Deal_Stand_Deliveries", d.done.Named("KEPT"), DealingMath.Deliveries.Named("ALL"), d.missed.Named("MISSED"))
                        : d.kind == DealKind.Work ? W(d.done == 0 ? "CC_Deal_Stand_Share" : "CC_Deal_Stand_Building")
                        : d.kind == DealKind.Market && d.done == 0 ? W("CC_Deal_Stand_Fee")
                        : (d.kind == DealKind.Proclaim && d.sub == (byte)ProclaimKind.Fair || d.kind == DealKind.Invite) && d.next >= 0
                            ? W("CC_Deal_Stand_Day", Feasts.DateText(d.home, Feasts.DayOf(d.home, d.next)).Named("DATE"))
                        : W("CC_Deal_Stand_Running");
                    break;
                case DealStage.Kept: stand = W(d.kind == DealKind.Work ? "CC_Deal_Stand_Built" : "CC_Deal_Stand_Kept"); break;
                case DealStage.Broken: stand = W("CC_Deal_Stand_Broken"); break;
                default: stand = W("CC_Deal_Stand_Ended"); break;
            }
            return W("CC_Deal_Line", what.Named("DEAL"), with.Named("WITH"), terms.Named("TERMS"), stand.Named("STAND"));
        }

        /// <summary>The town card's lines: the colony's dealings with that town, and what stands between them.</summary>
        public static List<string> TownLines(Settlement town)
        {
            var list = new List<string>();
            var book = Book;
            if (town == null || book == null) return list;
            foreach (var d in book.deals.Where(x => x.town == town.ID && (x.Live || x.stage == DealStage.Kept && x.kind == DealKind.Work)).OrderBy(x => x.Live ? 0 : 1))
                list.Add(Line(d));
            return list;
        }
    }
}
