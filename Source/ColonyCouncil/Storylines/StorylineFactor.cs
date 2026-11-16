using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // S1, the factor's goods (docs/quests-proposals.md; words in Defs/MissionDefs/Rimfolk_Storyline_Factor.xml). A factor of a
    // trading people (the road league first, any people that sends trade caravans else) asks the colony to keep her crates
    // for a season: under a roof, and none opened, sold or lost. Part 1, her clerk counts them at the end. Kept: part 2, a
    // rival trader comes to the gate and offers to buy them behind her back (a card: sell, refuse, or refuse and tell her);
    // then she comes for them, pays for the keeping and brings a share of her trade every season for a year, or finds them
    // sold (part 3, her debt, or her guards at the gate if it isn't paid). Some lost: part 2, ten days to pay what they were
    // worth, or her people send no traders for a year. The crates are the road league's sealed crates (Peoples/Events/
    // PeopleTrust.cs), with a trust record of their own so opening, selling and burning are noted as they happen.

    /// <summary>The factor's goods beyond its parts: who asks, the crates, the rain, the endings on the map.</summary>
    public class FactorRules : StorylineRules
    {
        public override string Kind => StorylineMath.Factor;

        private static int Now => Find.TickManager.TicksGame;

        public override void DebugEnded(Storyline s, string part, string outcome)
        {
            if (outcome == "sold") s.Set("sold");
            else if (outcome == "told") s.Set("told");
            else if (outcome == "lost") s.amount = StorylineMath.LostDebt(s.worth / 3f);
        }

        /// <summary>A people whose traders come with caravans.</summary>
        public static bool Trading(Faction f) => f?.def != null && !f.def.caravanTraderKinds.NullOrEmpty();

        /// <summary>The road league (and any people of the traders' calling) first; else the brokers, or any people that sends trade caravans.</summary>
        public override Faction Giver(Map map) =>
            Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && Peoples.CallingOf(f) == PeoplesMath.Traders)
            ?? Arrivals.Friendly(f => VarietyUtil.OnGoodTerms(f) && (Peoples.CallingOf(f) == PeoplesMath.Brokers || Trading(f)));

        /// <summary>The rival: a trader of another trading people at peace, else of another people at peace, else another house of her own people.</summary>
        public static Faction Rival(Faction giver) =>
            Arrivals.Friendly(f => f != giver && Trading(f)) ?? Arrivals.Friendly(f => f != giver) ?? giver;

        public static TrustRecord Record(Storyline s) => s == null || s.trust < 0 ? null : PeopleEventsBook.Instance?.trusts?.FirstOrDefault(r => r != null && r.id == s.trust);

        /// <summary>Her crates still on the map, whatever became of what is inside (a spoiled one is still hers to take).</summary>
        public static List<TrustCrate> Here(Storyline s, Map map)
        {
            var rec = Record(s);
            if (rec == null || map == null) return new List<TrustCrate>();
            return rec.crates.OfType<TrustCrate>().Where(c => !c.Destroyed && c.MapHeld == map).ToList();
        }

        /// <summary>Her crates on the map, still sealed and dry: not opened, sold or spoiled.</summary>
        public static List<TrustCrate> Held(Storyline s, Map map)
        {
            var rec = Record(s);
            return rec == null ? new List<TrustCrate>() : Here(s, map).Where(c => rec.FateOf(c.thingIDNumber) == null).ToList();
        }

        /// <summary>A factor's crates are in the colony's care now (the league's own crates in trust wait until they're gone).</summary>
        public static bool CratesHeld => Storylines.Running.Any(s => s.kind == StorylineMath.Factor && Record(s) is TrustRecord r && !r.done);

        /// <summary>What her crates still held are worth.</summary>
        public static float HeldWorth(Storyline s, Map map) => Held(s, map).Sum(c => c.Worth);

        private static StorylinePart KeepPart => Storylines.PartDef(StorylineMath.Factor, "keep")?.GetModExtension<StorylinePart>();

        /// <summary>
        /// The crates brought in: made with a trust record of their own, carried in by her porters with her at their head (by
        /// a lander's pods for a people of the spacer age) and set down by the colony's stores.
        /// </summary>
        public static bool Bring(Storyline s, Map map)
        {
            var f = s.giver;
            var book = PeopleEventsBook.Instance;
            if (f == null || book == null || PeopleTrust.CrateDef == null || map == null) return false;
            var crates = (s.load ?? "").Split('|').Where(g => g.Contains(":")).Select(PeopleTrust.MakeCrate).Where(c => c != null).ToList();
            if (crates.Count == 0) return false;
            var rec = new TrustRecord
            {
                id = book.nextTrust++, faction = f, map = map, left = Now, due = -1, fee = s.fee, master = s.xName, means = PeopleEventsMath.Means(PeopleEvents.AgeOf(f))
            };
            foreach (var c in crates)
            {
                c.trust = rec.id;
                rec.crates.Add(c);
                rec.ids.Add(c.thingIDNumber);
            }
            book.trusts.Add(rec);
            while (book.trusts.Count > PeopleEventsMath.MaxTrusts && book.trusts.Any(r => r.done)) book.trusts.Remove(book.trusts.First(r => r.done));
            s.trust = rec.id;
            s.count = crates.Count;
            // She comes with two porters.
            var party = new List<Pawn>();
            if (s.x != null && StoryChains.CanVisit(s.x)) party.Add(s.x);
            for (int i = 0; i < 2; i++)
            {
                var p = Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_League_Wayfarer"), f, new FloatRange(20f, 50f));
                if (p != null) party.Add(p);
            }
            bool came = party.Count > 0 && Arrivals.Visit(party, map, f, PeoplesD.GateSpot(map), 6f) != null;
            var at = PeopleTrust.Stores(map);
            var things = crates.Cast<Thing>().ToList();
            try
            {
                if (rec.means == "lander" && at.IsValid) DropPodUtility.DropThingsNear(at, map, things, 110, false, false, true, false);
                else
                {
                    var bearer = came && s.x != null && PeoplesG.Up(s.x, map) ? s.x : null;
                    Couriers.Loot(things, map, bearer != null ? bearer.Position : at, f, bearer, at);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the factor's crates carried in", e);
                foreach (var t in things.Where(t => !t.Spawned && !t.Destroyed)) GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
            }
            FactionMemory.Note(f, "traded", Storylines.Label(s));
            return true;
        }

        /// <summary>The count: crates still sealed and dry, those lost (opened, sold, spoiled, burned, gone), and what the lost ones were worth.</summary>
        public static (int held, int lost, float lostWorth) Count(Storyline s, Map map)
        {
            var held = Held(s, map);
            int total = Mathf.Max(s.count, Record(s)?.ids.Count ?? 0);
            float lostWorth = Mathf.Max(0f, s.worth - held.Sum(c => c.Worth));
            return (held.Count, Mathf.Max(0, total - held.Count), lostWorth);
        }

        // ---------------- The rain ----------------

        private static int WetHours(Storyline s, int id)
        {
            string pre = id + ":";
            var e = s.wet.FirstOrDefault(w => w.StartsWith(pre));
            return e != null && int.TryParse(e.Substring(pre.Length), out int h) ? h : 0;
        }

        private static void SetWet(Storyline s, int id, int hours)
        {
            string pre = id + ":";
            s.wet.RemoveAll(w => w.StartsWith(pre));
            s.wet.Add(pre + hours);
        }

        /// <summary>
        /// Every hour while her crates are kept: one standing out in the rain or snow with no roof over it gets wet; told at
        /// its first hour and halfway; after <see cref="StorylineMath.WetHoursToSpoil"/> hours the goods inside are spoiled
        /// (noted on the trust record, so the count and the fetch know).
        /// </summary>
        public override void Hourly(Storyline s, Map map, int now)
        {
            var rec = Record(s);
            if (rec == null || rec.done || map.weatherManager == null) return;
            if (map.weatherManager.RainRate < 0.01f && map.weatherManager.SnowRate < 0.01f) return;
            var m = s.missionId >= 0 ? Missions.ById(s.missionId) : null;
            TrustCrate warn = null;
            foreach (var c in Held(s, map).Where(c => c.Spawned && c.Map == map && !c.Position.Roofed(map)))
            {
                int h = WetHours(s, c.thingIDNumber) + 1;
                SetWet(s, c.thingIDNumber, h);
                if (h >= StorylineMath.WetHoursToSpoil)
                {
                    rec.Note(c.thingIDNumber, "spoiled");
                    StorylineKit.Note(KeepPart, s, m, map, "spoiled", new LookTargets(c), MessageTypeDefOf.NegativeEvent);
                }
                else if (h == 1 || h == StorylineMath.WetHoursToSpoil / 2) warn ??= c;
            }
            if (warn != null) StorylineKit.Note(KeepPart, s, m, map, "wet", new LookTargets(warn), MessageTypeDefOf.CautionInput);
        }

        // ---------------- Endings ----------------

        public override bool Ending(Storyline s, string key, Map map, HappeningContext ctx)
        {
            var f = s.giver;
            switch (key)
            {
                case "share":
                {
                    if (MapFacts.ThreatOn(map)) return false;
                    // She comes for her crates and pays for the keeping, less what is missing since the count.
                    float missing = Mathf.Max(0f, s.worth - HeldWorth(s, map));
                    int fee = StorylineMath.FeeLess(s.fee, missing);
                    if (missing > 1f) s.Set("missing");
                    var x = Fetch(s, map, fee, true);
                    s.amount = fee;
                    StoryChains.Goodwill(f, s.Has("told") ? 12 : 8, GoodwillWhy.Task(10));
                    FactionMemory.Note(f, "keptWord", Storylines.Label(s));
                    // Every season for a year she comes by with a share of her trade.
                    Storylines.AddAfter(new StorylineAfter
                    {
                        kind = "share", faction = f, pawn = x != null && !x.Dead && x.Faction == f ? x : null, who = s.xName, left = StorylineMath.Shares,
                        amount = StorylineMath.SeasonShare(s.worth, s.Has("told")), next = Now + Mathf.RoundToInt(StorylineMath.SeasonDays * GenDate.TicksPerDay)
                    });
                    return true;
                }
                case "neverknew":
                {
                    if (MapFacts.ThreatOn(map)) return false;
                    // She finds them gone and believes they were stolen: no fee, and she trusts the colony a little less.
                    Visit(s, map, 4f);
                    Close(s, "red");
                    StoryChains.Goodwill(f, -5, GoodwillWhy.Task(-5));
                    return true;
                }
                case "debtpaid":
                    Close(s, "red");
                    StoryChains.Goodwill(f, -8, GoodwillWhy.Task(-8));
                    return true;
                case "guards":
                {
                    if (MapFacts.ThreatOn(map)) return false;
                    // Her guards come to the gate with her word: no more trade with the colony for a year.
                    var guards = new List<Pawn>();
                    for (int i = 0; i < 3; i++)
                    {
                        var g = Arrivals.Make(PeoplesD.FighterKind(f), f, new FloatRange(22f, 45f));
                        if (g != null) guards.Add(g);
                    }
                    if (guards.Count > 0 && Arrivals.Visit(guards, map, f, PeoplesD.GateSpot(map), 5f) != null)
                    {
                        s.group.AddRange(guards);
                        StorylineKit.Say(guards[0], Storylines.PartDef(StorylineMath.Factor, "debt"), s, null, "guards");
                    }
                    Close(s, "red");
                    StoryChains.Goodwill(f, -40, GoodwillWhy.Task(-40));
                    FactionMemory.Note(f, "robbed", Storylines.Label(s));
                    NoTrade(f);
                    return true;
                }
                case "madegood":
                    if (MapFacts.ThreatOn(map)) return false;
                    Fetch(s, map, 0, false);
                    StoryChains.Goodwill(f, 4, GoodwillWhy.Task(4));
                    return true;
                case "unpaid":
                    if (MapFacts.ThreatOn(map)) return false;
                    Fetch(s, map, 0, false);
                    StoryChains.Goodwill(f, -15, GoodwillWhy.Task(-15));
                    FactionMemory.Note(f, "wordBroken", Storylines.Label(s));
                    NoTrade(f);
                    return true;
                case StorylineMath.Faded:
                    // It could not go on: she comes for whatever of hers the colony still keeps.
                    if (Record(s) is TrustRecord r && !r.done && Here(s, map).Count > 0)
                    {
                        if (MapFacts.ThreatOn(map)) return false;
                        Fetch(s, map, 0, false);
                    }
                    Close(s, "lapsed");
                    return true;
            }
            Close(s, "lapsed");
            return true;
        }

        /// <summary>No traders of her people for a year.</summary>
        private static void NoTrade(Faction f) =>
            Storylines.AddAfter(new StorylineAfter { kind = "notrade", faction = f, until = Now + Mathf.RoundToInt(StorylineMath.NoTradeDays * GenDate.TicksPerDay) });

        /// <summary>Her people are gone or at war with the colony: nobody will come for the crates, so the colony opens them.</summary>
        public override void Gone(Storyline s, Map map)
        {
            var rec = Record(s);
            if (rec == null || rec.done) return;
            var crates = Held(s, map).Where(c => c.Spawned).ToList();
            rec.done = true;
            rec.ink = "lapsed";
            foreach (var c in crates)
            {
                var at = c.Position;
                foreach (var t in c.Contents().SelectMany(g => Couriers.Make(g.def, g.count))) GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
                c.Destroy(DestroyMode.Vanish);
            }
            if (crates.Count > 0) s.Set("opened");
        }

        /// <summary>Done with the crates: the trust record closed, so the league's own trust can come again.</summary>
        private static void Close(Storyline s, string ink)
        {
            var rec = Record(s);
            if (rec == null || rec.done) return;
            rec.done = true;
            rec.ink = ink;
        }

        /// <summary>She (or someone of hers in her place) walks in and stays a few hours at the gate.</summary>
        private static Pawn Visit(Storyline s, Map map, float hours, List<Pawn> with = null)
        {
            var f = s.giver;
            var x = s.x != null && StoryChains.CanVisit(s.x) ? s.x : Arrivals.Make(PeoplesD.TraderKind(f), f, new FloatRange(30f, 60f));
            if (x == null) return null;
            var party = new List<Pawn> { x };
            if (with != null) party.AddRange(with.Where(p => p != null));
            return Arrivals.Visit(party, map, f, PeoplesD.GateSpot(map), hours) != null ? x : null;
        }

        /// <summary>
        /// She comes for her crates with porters: every crate of hers still on the map is carried out to her (a spoiled one
        /// too), and she pays <paramref name="fee"/> for the keeping. The trust record is closed. Returns who came.
        /// </summary>
        public static Pawn Fetch(Storyline s, Map map, int fee, bool good)
        {
            var f = s.giver;
            var here = Here(s, map);
            var porters = new List<Pawn>();
            for (int i = 0; i < Mathf.Clamp(Mathf.CeilToInt(here.Count / 3f), 1, 2); i++) porters.Add(Arrivals.Make(PeoplesD.KindNamed(f, "Rimfolk_League_Wayfarer"), f, new FloatRange(20f, 50f)));
            Pawn x = null;
            try
            {
                x = Visit(s, map, 10f, porters);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("storyline: the factor at the gate", e);
            }
            if (here.Count > 0)
            {
                foreach (var c in here.Where(c => c.Spawned)) c.SetForbidden(false, false);
                var ctx = new HappeningContext { map = map, faction = f, stranger = x, incident = Storylines.PartDef(StorylineMath.Factor, "keep"), count = here.Count };
                // apart: a colonist carries the crates out to the factor at the gate, who takes them.
                new DilemmaEffect_CarryOut { what = "Rimfolk_TrustCrate", count = here.Count, steward = false, who = PlayWho.None, label = "CC_Storyline_CratesLabel".Translate().Resolve() }.Apply(ctx);
            }
            if (fee > 0) Couriers.PaySilver(map, x != null && x.Spawned ? x.Position : IntVec3.Invalid, fee, f, x);
            Close(s, good ? "good" : "red");
            if (x != null && x == s.x) StorylineKit.Say(x, Storylines.PartDef(StorylineMath.Factor, "keep"), s, null, good ? "thanks" : "cold");
            return x;
        }

        /// <summary>A season's share of her trade, brought to the gate by her (or someone of hers).</summary>
        public static void Share(StorylineAfter a, Map map)
        {
            var f = a.faction;
            var x = a.pawn != null && StoryChains.CanVisit(a.pawn) ? a.pawn : Arrivals.Make(PeoplesD.TraderKind(f), f, new FloatRange(30f, 60f));
            bool came = x != null && Arrivals.Visit(new List<Pawn> { x }, map, f, PeoplesD.GateSpot(map), 5f) != null;
            Couriers.PaySilver(map, came ? x.Position : DropCellFinder.TradeDropSpot(map), a.amount, f, came ? x : null);
            string who = came ? x.LabelShort : a.who ?? f.Name;
            ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, map, "CC_Storyline_Share".Translate(who.Named("WHO"), f.Name.Named("FACTION"), a.amount.Named("SILVER")).Resolve(),
                came ? new LookTargets(x) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }
    }

    // ===================== Part 1: keep the factor's crates =====================

    /// <summary>
    /// She asks the colony to keep her crates (five to eight, the goods of her people's age) until the end of the season.
    /// Taken on, she comes with porters and they are set down by the stores; at the end of the keeping her clerk comes and
    /// counts them. All there, sealed and dry: kept. Any opened, sold, spoiled in the rain, burned or gone: lost.
    /// </summary>
    public class HappeningWorker_FactorKeep : HappeningWorker_StorylinePart
    {
        protected override int Rating => 2;

        protected override bool Possible(Map map) =>
            Storylines.Rules(StorylineMath.Factor).Giver(map) != null && PeopleTrust.CrateDef != null && map.zoneManager.AllZones.OfType<Zone_Stockpile>().Any()
            && !(PeopleEventsBook.Instance?.trusts?.Any(r => r != null && !r.done) ?? false);

        protected override string Silver(Mission m, Storyline s) => s?.fee.ToString();

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.making;
            var map = ctx.map;
            var f = Storylines.Rules(StorylineMath.Factor).Giver(map);
            if (s == null || f == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            var x = Arrivals.Make(PeoplesD.TraderKind(f), f, new FloatRange(30f, 60f));
            if (x == null) return false;
            Arrivals.Hold(x);
            int age = PeopleEvents.AgeOf(f);
            var load = PeopleTrust.Load(age, StorylineMath.FactorCrates(Rand.Value));
            s.giver = f;
            s.x = x;
            s.xName = x.LabelShort;
            s.place = LivingEvents.TownOf(f, map) ?? f.Name;
            s.count = load.Count;
            s.load = string.Join("|", load);
            s.worth = Mathf.RoundToInt(PeopleTrust.Worth(load));
            s.fee = StorylineMath.FactorFee(s.worth);
            m.faction = f;
            m.place = s.place;
            m.x = x;
            m.reward = s.fee;
            m.goodwill = 6;
            m.days = Rand.Range(StorylineMath.KeepMinDays, StorylineMath.KeepMaxDays);
            Fill(m, ctx);
            ctx.stranger = x;
            ctx.count = s.count;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s == null || !FactorRules.Bring(s, map)) return false;
            m.stage = 0;
            m.lastLook = Find.TickManager.TicksGame;
            StorylineKit.Accepted(P, s, m, map, s.x != null && s.x.Spawned ? new LookTargets(s.x) : null, s.fee.ToString());
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            StorylineKit.Remind(P, s, m, map, now);
            if (m.stage == 0 && now >= m.deadline)
            {
                // Her clerk comes to count the crates where they stand.
                var spot = FactorRules.Held(s, map).FirstOrDefault(c => c.Spawned)?.Position ?? PeopleTrust.Stores(map);
                var clerk = Arrivals.Make(Arrivals.PersonKind(s.giver), s.giver, new FloatRange(20f, 50f));
                if (clerk != null && Arrivals.Visit(new List<Pawn> { clerk }, map, s.giver, spot.IsValid ? spot : (IntVec3?)null, 4f) != null) m.group.Add(clerk);
                m.stage = 1;
                m.due = now + GenDate.TicksPerHour * 3;
                Note(m, map, "clerk", clerk != null && clerk.Spawned ? new LookTargets(clerk) : LookTargets.Invalid);
                return;
            }
            if (m.stage != 1) return;
            var counter = m.group.FirstOrDefault(p => p != null && p.Spawned && !p.Dead);
            var held = FactorRules.Held(s, map);
            bool there = counter != null && held.Any(c => c.Spawned && c.Position.InHorDistOf(counter.Position, 10f));
            if (!there && now < m.due) return;
            var (n, lost, lostWorth) = FactorRules.Count(s, map);
            s.lost = lost;
            var look = counter != null ? new LookTargets(counter) : LookTargets.Invalid;
            if (lost == 0)
            {
                Missions.Goodwill(m, m.goodwill);
                Say(counter, m, "kept");
                End(m, MissionState.Done, "kept", look, null, s.fee.ToString());
                return;
            }
            // What was lost, to make good: its worth.
            s.amount = StorylineMath.LostDebt(lostWorth);
            Say(counter, m, "lost");
            End(m, MissionState.Failed, "lost", look, lost.ToString(), s.amount.ToString());
        }

        public override string Status(Mission m)
        {
            var s = Story(m);
            var map = m.Map;
            if (s == null || map == null) return null;
            var held = FactorRules.Held(s, map);
            int wet = held.Count(c => c.Spawned && !c.Position.Roofed(map));
            string line = "CC_Storyline_Factor_Status".Translate(held.Count.Named("HELD"), s.count.Named("TOTAL")).Resolve();
            if (wet > 0) line += " " + "CC_Storyline_Factor_Unroofed".Translate(wet.Named("COUNT")).Resolve();
            return line;
        }

        public override LookTargets Targets(Mission m)
        {
            var s = Story(m);
            var here = s == null ? null : FactorRules.Held(s, m.Map).Where(c => c.Spawned).Cast<Thing>().ToList();
            return here != null && here.Count > 0 ? new LookTargets(here) : base.Targets(m);
        }
    }

    // ===================== Part 2: the rival's offer =====================

    /// <summary>
    /// The crates were kept well, and a trader of another people (a rival of hers) asks to meet at the gate. Taken on, the
    /// rival comes, and a card asks: sell the crates (the rival pays well over their worth and they are carried out), refuse,
    /// or refuse and send word to the factor (her people think better of the colony; the rival's a little worse).
    /// </summary>
    public class HappeningWorker_FactorRival : HappeningWorker_StorylinePart
    {
        public const string Card = "Rimfolk_Storyline_FactorOffer";

        protected override bool Possible(Map map)
        {
            var s = Storylines.current;
            return s != null && FactorRules.Held(s, map).Count > 0;
        }

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            var other = FactorRules.Rival(s.giver);
            var y = Arrivals.Make(PeoplesD.TraderKind(other), other, new FloatRange(25f, 55f));
            if (y == null) return false;
            Arrivals.Hold(y);
            s.other = other;
            s.y = y;
            s.yName = y.LabelShort;
            if (other == s.giver) s.Set("samepeople");
            s.amount = StorylineMath.RivalOffer(FactorRules.HeldWorth(s, map));
            m.faction = s.giver;
            m.place = s.place;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            m.x = y;
            m.days = 2f;
            Fill(m, ctx);
            ctx.stranger = s.x;
            ctx.count = FactorRules.Held(s, map).Count;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            var s = Story(m);
            if (s?.y == null || s.y.Dead) return false;
            if (Arrivals.Visit(new List<Pawn> { s.y }, map, s.other ?? s.giver, PeoplesD.GateSpot(map), 12f) == null) return false;
            m.stage = 0;
            StorylineKit.Accepted(P, s, m, map, new LookTargets(s.y));
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            var y = s?.y;
            if (s == null || y == null || y.Dead || y.Discarded)
            {
                End(m, MissionState.Done, "refused");
                return;
            }
            if (m.stage == 0)
            {
                var gate = PeoplesD.GateSpot(map);
                bool there = y.Spawned && (gate == null || y.Position.InHorDistOf(gate.Value, 14f) || now - m.started > GenDate.TicksPerHour * 4);
                if (!there)
                {
                    if (now - m.started > GenDate.TicksPerDay) End(m, MissionState.Done, "refused");
                    return;
                }
                int crates = FactorRules.Held(s, map).Count;
                bool asked = StorylineKit.Ask(Card, s, m, map, c =>
                {
                    c.stranger = y;
                    c.faction = s.other ?? s.giver;
                    c.count = crates;
                });
                if (asked)
                {
                    m.stage = 1;
                    m.lastLook = now;
                    Say(y, m, "offer");
                }
                else if (now - m.started > GenDate.TicksPerDay) End(m, MissionState.Done, "refused");
                return;
            }
            // The card was answered (Answered ends the part); a card lost without an answer, or the time run out, is a refusal.
            if (now >= m.deadline || !StorylineKit.CardOut(s) && now - m.lastLook > GenDate.TicksPerHour * 2)
            {
                Arrivals.SendAway(new List<Pawn> { y });
                End(m, MissionState.Done, "refused");
            }
        }

        public override void Answered(Mission m, Storyline s, HappeningContext ctx, int option, bool byDefault)
        {
            var map = m.Map;
            var y = s.y;
            switch (option)
            {
                case 0:
                {
                    // Sold: the rival pays now, and the crates are carried out to them (the card's own carrying). Noted as sold.
                    var rec = FactorRules.Record(s);
                    foreach (var c in FactorRules.Held(s, map)) rec?.Note(c.thingIDNumber, PeopleEventsMath.Sold);
                    Couriers.PaySilver(map, y != null && y.Spawned ? y.Position : IntVec3.Invalid, s.amount, s.other ?? s.giver, y != null && y.Spawned ? y : null);
                    s.Set("sold");
                    Say(y, m, "sold");
                    End(m, MissionState.Done, "sold", y != null && y.Spawned ? new LookTargets(y) : null, null, s.amount.ToString());
                    break;
                }
                case 2:
                    // Refused, and word sent to her: she thinks better of the colony, the rival's people a little worse.
                    s.Set("told");
                    Say(y, m, "told");
                    if (y != null) Arrivals.SendAway(new List<Pawn> { y });
                    StoryChains.Goodwill(s.giver, 8, GoodwillWhy.Task(8));
                    if (s.other != null && s.other != s.giver) StoryChains.Goodwill(s.other, -5, GoodwillWhy.Task(-5));
                    End(m, MissionState.Done, "told", y != null && y.Spawned ? new LookTargets(y) : null);
                    break;
                default:
                    s.Set("refused");
                    Say(y, m, "refused");
                    if (y != null) Arrivals.SendAway(new List<Pawn> { y });
                    End(m, MissionState.Done, "refused", y != null && y.Spawned ? new LookTargets(y) : null);
                    break;
            }
        }

        public override void Describe(Mission m, Storyline s, string choice, HappeningContext ctx, ImpactSheet sheet)
        {
            switch (choice)
            {
                case "sell":
                    sheet.Silver(s.amount);
                    sheet.Add(ImpactKind.Goodwill, -1, "CC_Storyline_Impact_Sell".Translate(s.xName.Named("GIVER")).Resolve(), "CC_Storyline_Impact_SellBrief".Translate().Resolve(), "slsell");
                    break;
                case "refuse":
                    sheet.Play("CC_Storyline_Impact_Refuse".Translate(s.xName.Named("GIVER"), s.fee.Named("FEE")).Resolve(), "CC_Storyline_Impact_RefuseBrief".Translate().Resolve());
                    break;
                case "tell":
                    sheet.Goodwill(s.giver, 8);
                    if (s.other != null && s.other != s.giver) sheet.Goodwill(s.other, -5);
                    sheet.Play("CC_Storyline_Impact_Tell".Translate(s.xName.Named("GIVER")).Resolve(), "CC_Storyline_Impact_RefuseBrief".Translate().Resolve());
                    break;
            }
        }

        public override string Status(Mission m) =>
            m.stage == 0 ? "CC_Storyline_Rival_Coming".Translate((Story(m)?.yName ?? "?").Named("RIVAL")).Resolve() : "CC_Storyline_Rival_Asked".Translate().Resolve();
    }

    // ===================== Paying her: what was lost, or the crates sold =====================

    /// <summary>
    /// Silver owed to the factor, by a date: once it is in the stockpiles her people come for it within half a day (by the
    /// couriers, else taken from the stores). Paid, or not by the date.
    /// </summary>
    public abstract class HappeningWorker_FactorPay : HappeningWorker_StorylinePart
    {
        protected abstract int Owed(Storyline s, Map map);

        protected abstract float Days { get; }

        protected override bool Possible(Map map) => Storylines.current?.giver != null;

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var s = Storylines.current;
            var map = ctx.map;
            if (s?.giver == null) return false;
            m.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (m.a == null) return false;
            s.amount = Owed(s, map);
            m.faction = s.giver;
            m.place = s.place;
            m.goals.Add(new MissionGoal { what = ThingDefOf.Silver.defName, count = s.amount });
            m.days = Days;
            m.penalty = 0;
            Fill(m, ctx);
            ctx.stranger = s.x;
            ctx.count = s.lost;
            return true;
        }

        public override bool Start(Mission m, Map map)
        {
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            StorylineKit.Accepted(P, Story(m), m, map, null);
            return true;
        }

        public override void Check(Mission m, Map map, int now)
        {
            var s = Story(m);
            if (s == null)
            {
                Missions.Finish(m, MissionState.Failed, "ended", null);
                return;
            }
            foreach (var g in m.goals) g.have = Missions.Count(map, g.what);
            bool all = m.goals.All(g => g.have >= g.count);
            StorylineKit.Remind(P, s, m, map, now);
            if (m.stage == 0 && all)
            {
                m.stage = 1;
                if (m.due < 0)
                {
                    m.due = Mathf.Min(now + GenDate.TicksPerDay / 2, m.deadline);
                    Note(m, map, "ready", LookTargets.Invalid, MessageTypeDefOf.PositiveEvent, null, s.amount.ToString(), TextUtil.DaysString(m.due - now));
                }
            }
            else if (m.stage == 1 && !all) m.stage = 0;
            if (m.stage == 1 && now >= m.due || now >= m.deadline && all)
            {
                // Her people come for it and carry it off (Couriers/); else it's taken from the stores now.
                if (!Couriers.CollectGoals(map, m.goals.Select(g => new KeyValuePair<string, int>(g.what, g.count)).ToList(), s.giver, 0))
                    foreach (var g in m.goals)
                        Missions.Take(map, g.what, g.count);
                End(m, MissionState.Done, "paid", null, null, s.amount.ToString());
                return;
            }
            if (now >= m.deadline) End(m, MissionState.Failed, "unpaid", null, null, s.amount.ToString());
        }

        public override string Status(Mission m) =>
            m.stage == 1 ? "CC_Storyline_Pay_Coming".Translate((m.faction?.Name ?? "?").Named("FACTION"), TextUtil.DaysString(Mathf.Max(0, m.due - Find.TickManager.TicksGame)).Named("DAYS")).Resolve() : null;
    }

    /// <summary>Part 2 when crates were lost: what they were worth, within ten days.</summary>
    public class HappeningWorker_FactorMakeGood : HappeningWorker_FactorPay
    {
        protected override float Days => StorylineMath.MakeGoodDays;

        /// <summary>Counted at part 1's end (the clerk's count).</summary>
        protected override int Owed(Storyline s, Map map) => Mathf.Max(20, s.amount);
    }

    /// <summary>Part 3 when the crates were sold and she found out: their whole worth.</summary>
    public class HappeningWorker_FactorDebt : HappeningWorker_FactorPay
    {
        protected override float Days => StorylineMath.DebtDays;

        protected override int Owed(Storyline s, Map map) => StorylineMath.SoldDebt(s.worth);
    }
}
