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
    /// <summary>
    /// The colony calls in a debt of honour (docs/design/stage2-diplomacy.md, round a, item 1): from a letter or a call to
    /// their notable (DiplomacyLetters.cs; the Diplomacy page and the town panel open that letter), or face to face with
    /// one of theirs at the colony (DiplomacyActs.cs). What they can do now, each greyed with why not, by the debt's size
    /// (<see cref="SizeOf"/>, half for a notable's personal favour: the player, 10 November 2026, "appropriate to the debt
    /// i actually owe"): a load of goods worth 30 silver a point by their town's fortune and their means, brought by their
    /// porters; a word for the colony with a third people (half the size in goodwill, said in that people's letter); a
    /// colonist of the colony they hold, set down at the edge of the colony's land to walk home (15 or more); their help
    /// toward peace with a people at war with the colony (a promise the truce's answer reads: Truces.cs; 12 or more); their
    /// fighters while a threat is on a home (their band walks in some hours later, as a defence pact's does: Pacts.cs,
    /// release 2a.2, decision 3; 12 or more). One debt of a people called in a season, by letter or face to face
    /// (<see cref="CalledLately"/>). They honour it while their head (or the notable who owes) thinks well
    /// of the colony (20 or more), seven times in ten at a lower regard, four in ten below 0; a refusal is their letter (or
    /// their word face to face) and the deed "brokeWord".
    /// </summary>
    public static class FavourCalls
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>One thing they could do now: what, in words, and why not when it can't be.</summary>
        public sealed class Option
        {
            public FavourAsk ask;
            public string arg;
            public string label;
            public string why;

            /// <summary>Another purpose's own code for it (a truce's price, a trade pact's cart), or null for a debt's ask.</summary>
            public string code;

            /// <summary>Its kind's place in the list (a debt's: goods, a word, a captive, peace, fighters); 0 for another purpose's.</summary>
            public int group;

            /// <summary>What it does and how much, in a line ("they put in a word with the Verea Herd: their goodwill toward the colony rises a little"), or null.</summary>
            public string what;

            /// <summary>Folded under "Others" (a word with a people it would help less).</summary>
            public bool more;

            public string Code => code ?? FavourMath.AskCode(ask, arg);
        }

        // ================= Whether it can be called in =================

        /// <summary>Why this debt of theirs can't be called in now, or null.</summary>
        public static string WhyNot(Tie t)
        {
            if (t == null || !t.Live || FavoursOwed.ColonyOwes(t)) return DipWords.P("CC_Dip_Why_Nothing");
            if (!Diplomacy.FavoursOn) return DipWords.P("CC_Dip_Why_Off");
            if (t.state == Tie.State.CalledIn) return DipWords.P("CC_Dip_Why_Called");
            var f = FavoursOwed.PeopleOf(t);
            if (f == null || f.defeated) return DipWords.P("CC_Dip_Why_Gone");
            if (Diplomacy.Hostile(f)) return DipWords.P("CC_Dip_Why_War", DipWords.People(f).Named("PEOPLE"));
            if (FavoursOwed.IsPersonal(t))
            {
                var n = FavoursOwed.NotableOf(t);
                if (n == null || !n.Alive) return DipWords.P("CC_Dip_Why_Gone");
                if (!KinMemory.Ours(WorldLedger.PawnOf(t.to))) return DipWords.P("CC_Dip_Why_OwedGone");
            }
            if (CalledLately(f)) return DipWords.P("CC_Dip_Why_CalledLately", DipWords.People(f).Named("PEOPLE"));
            return null;
        }

        /// <summary>
        /// A debt of this people (or of one of their notables in person) was called in this season, by letter or face to face:
        /// one asked and not yet answered, or one honoured or refused within <see cref="FavourMath.CallGapDays"/>.
        /// </summary>
        public static bool CalledLately(Faction f)
        {
            if (f == null) return false;
            int now = Now;
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Favour))
            {
                if (t == null || t.origin != FavourMath.Origin || FavoursOwed.ColonyOwes(t)) continue;
                bool called = t.state == Tie.State.CalledIn || (t.state == Tie.State.Kept || t.state == Tie.State.Broken) && t.closed >= 0 && !FavourMath.CallAllowed(t.closed, now);
                if (called && FavoursOwed.PeopleOf(t) == f) return true;
            }
            return false;
        }

        /// <summary>A debt's size (FavourMath.SizeOf): the weight of the good turn that made it, else that of its kind.</summary>
        public static int SizeOf(Tie t)
        {
            if (t?.terms == null) return 0;
            int kind = t.terms.key != null && FactionMemory.Kinds.TryGetValue(t.terms.key, out var k) && k != null ? k.weight : 0;
            return FavourMath.SizeOf(t.terms.number, kind);
        }

        /// <summary>What it counts for in what can be asked: its size, half for a notable's personal favour.</summary>
        public static int Worth(Tie t) => FavourMath.Effective(SizeOf(t), FavoursOwed.IsPersonal(t));

        /// <summary>"a small debt", "a debt", "a great debt": its weight in words.</summary>
        public static string WeightWords(Tie t) => DipWords.P("CC_Dip_Weight_" + FavourMath.WeightOf(SizeOf(t)));

        /// <summary>Why this much can't be asked on so small a debt, or null.</summary>
        private static string SizeWhy(FavourAsk a, Tie t) =>
            FavourMath.Allows(a, Worth(t)) ? null : DipWords.P(FavoursOwed.IsPersonal(t) ? "CC_Dip_Why_TooSmallPersonal" : "CC_Dip_Why_TooSmall", WeightWords(t).Named("WEIGHT"));

        /// <summary>
        /// What they could do for this debt, each with what it does and how much, and why not when it can't be now (the player,
        /// 11 November 2026: "this favour menu is also a bit overwhelming and i'm not really sure what many of these options
        /// really do"): goods by who owes and what they have; a word with the few peoples it would help most (the rest folded
        /// under "Others"); a captive of the colony's they hold; their help toward peace; their fighters. While the debt can't
        /// be called in now (called lately, their people at war, the one who owes gone), no option is offered as if it could
        /// be: each is greyed with that why (the player: ""a word with" should probably only be listed when the favour can
        /// actually be called in, same as for others").
        /// </summary>
        public static List<Option> Options(Tie t, Map home)
        {
            var list = new List<Option>();
            var f = FavoursOwed.PeopleOf(t);
            if (f == null) return list;
            home ??= FavoursOwed.HomeOf(t);
            string who = DebtorName(t, f);
            // A load of goods, by who owes and what they have.
            var from = FromOf(t, f);
            foreach (var kind in FavourMath.GoodsKindsFrom(from, Ages.Of(f), Peoples.CallingOf(f)))
            {
                var (def, count) = Goods(kind, f, t);
                string goods = def != null ? Couriers.Amount(count, null, def) : "";
                list.Add(new Option
                {
                    ask = FavourAsk.Goods, arg = kind, group = 0, why = def == null ? DipWords.P("CC_Dip_Why_NoGoods") : null,
                    label = DipWords.P("CC_Dip_Opt_Goods", goods.Named("GOODS"), DipWords.People(f).Named("PEOPLE")),
                    what = def == null ? null : DipWords.P("CC_Dip_What_Goods_" + from, who.Named("WHO"), goods.Named("GOODS"), Mathf.RoundToInt(def.BaseMarketValue * count).Named("SILVER"),
                        DipWords.People(f).Named("PEOPLE"))
                });
            }
            // A word with the third peoples it would help most: known to the colony, on good terms with them, not yet at full
            // goodwill; the coolest and the nearest first, a handful shown, the rest under "Others".
            int gw = FavourMath.WordGoodwill(Worth(t));
            var friends = WordPeoples(f, home);
            if (friends.Count == 0)
                list.Add(new Option { ask = FavourAsk.Word, group = 1, label = DipWords.P("CC_Dip_Opt_WordAny"), why = DipWords.P("CC_Dip_Why_NoFriends", DipWords.People(f).Named("PEOPLE")) });
            for (int i = 0; i < friends.Count; i++)
            {
                var o = friends[i];
                list.Add(new Option
                {
                    ask = FavourAsk.Word, arg = Refs.Of(o).ToString(), group = 1, more = i >= FavourMath.WordsShown,
                    label = DipWords.P("CC_Dip_Opt_Word", DipWords.People(o).Named("PEOPLE"), WordWords(Worth(t)).Named("HOWMUCH")),
                    what = DipWords.P("CC_Dip_What_Word", who.Named("WHO"), DipWords.People(o).Named("PEOPLE"), DipWords.P("CC_Dip_What_Much_" + FavourMath.WordMuchKey(gw)).Named("HOWMUCH"),
                        gw.Named("GOODWILL"))
                });
            }
            // A colonist of the colony they hold.
            var held = Held(f);
            if (held.Count == 0)
                list.Add(new Option { ask = FavourAsk.Captive, group = 2, label = DipWords.P("CC_Dip_Opt_CaptiveAny"), why = DipWords.P("CC_Dip_Why_NoCaptive", DipWords.People(f).Named("PEOPLE")) });
            foreach (var p in held)
                list.Add(new Option
                {
                    ask = FavourAsk.Captive, arg = Refs.Of(p).ToString(), group = 2, label = DipWords.P("CC_Dip_Opt_Captive", p.Named("PAWN")),
                    why = SizeWhy(FavourAsk.Captive, t) ?? (home == null ? DipWords.P("CC_Dip_Why_NoHome") : null),
                    what = DipWords.P("CC_Dip_What_Captive", p.Named("PAWN"), DipWords.People(f).Named("PEOPLE"))
                });
            // Their help toward peace with a people at war with the colony.
            var enemies = Enemies(f).ToList();
            if (enemies.Count == 0)
                list.Add(new Option { ask = FavourAsk.Peace, group = 3, label = DipWords.P("CC_Dip_Opt_PeaceAny"), why = DipWords.P("CC_Dip_Why_NoEnemy", DipWords.People(f).Named("PEOPLE")) });
            foreach (var e in enemies)
                list.Add(new Option
                {
                    ask = FavourAsk.Peace, arg = Refs.Of(e).ToString(), group = 3, label = DipWords.P("CC_Dip_Opt_Peace", DipWords.People(e).Named("PEOPLE")),
                    why = SizeWhy(FavourAsk.Peace, t) ?? (PeacePromised(f, e) != null ? DipWords.P("CC_Dip_Why_Promised", DipWords.People(f).Named("PEOPLE")) : null),
                    what = DipWords.P("CC_Dip_What_Peace", DipWords.People(f).Named("PEOPLE"), DipWords.People(e).Named("ENEMY"))
                });
            // Their fighters, only while a threat is on a home (release 2a.2).
            list.Add(new Option
            {
                ask = FavourAsk.Fighters, arg = home != null ? Homes.KeyOf(home).ToString() : "", group = 4, label = DipWords.P("CC_Dip_Opt_Fighters", DipWords.People(f).Named("PEOPLE")),
                why = SizeWhy(FavourAsk.Fighters, t) ?? FightersWhy(f, home),
                what = DipWords.P("CC_Dip_What_Fighters", DipWords.People(f).Named("PEOPLE"))
            });
            // The debt itself can't be called in now: nothing is offered as if it could be.
            string gate = WhyNot(t);
            if (gate != null)
                foreach (var o in list)
                    o.why = gate;
            return list;
        }

        /// <summary>The one who owes it, for its words: the notable in person, else their people.</summary>
        private static string DebtorName(Tie t, Faction f) => FavoursOwed.NotableOf(t)?.Name ?? DipWords.People(f);

        /// <summary>Who owes it, for what goods they have: their people's stores, or the notable in person by their trade (a trader, a healer, a farmer...).</summary>
        public static GoodsFrom FromOf(Tie t, Faction f)
        {
            var n = FavoursOwed.NotableOf(t);
            if (n == null) return GoodsFrom.Stores;
            var p = n.pawn;
            int Skill(SkillDef s) => p?.skills?.GetSkill(s)?.Level ?? 0;
            bool trader = n.role == NotableRole.Trader || n.met == "trader" || p?.TraderKind != null;
            return FavourMath.From(true, trader, Skill(SkillDefOf.Medicine), Skill(SkillDefOf.Plants), Skill(SkillDefOf.Animals), Skill(SkillDefOf.Crafting));
        }

        /// <summary>
        /// The peoples a word with would help most: on good terms with them and not at war with the colony, known to the colony
        /// (one of theirs known by name, their deeds remembered, or a town of theirs within reach), not yet at full goodwill
        /// toward it; the coolest toward the colony and the nearest first.
        /// </summary>
        public static List<Faction> WordPeoples(Faction f, Map home)
        {
            var list = new List<Faction>();
            foreach (var o in ThirdPeoples(f))
                if (Known(o) && FavourMath.WordHelps(o.PlayerGoodwill))
                    list.Add(o);
            return list.OrderBy(o => FavourMath.WordRank(o.PlayerGoodwill, home != null ? WorldSituationWorker.Distance(home, WorldSituationWorker.NearestTown(o, home)) : 0f)).ToList();
        }

        /// <summary>The colony knows this people: one of theirs by name, their deeds in its memory, or a town of theirs within a home's reach.</summary>
        private static bool Known(Faction o)
        {
            try
            {
                if (NotableBook.Instance?.notables.Any(n => n != null && n.faction == o) ?? false) return true;
                if (FactionMemory.Instance?.Of(o).Any() ?? false) return true;
                foreach (var r in RegionTowns.Reach)
                    if (r?.towns != null && r.towns.Any(s => s?.Faction == o))
                        return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favours: a people known", e);
            }
            return false;
        }

        /// <summary>
        /// The menu of what can be asked (the player's plan, 11 November 2026: "a short list in a few kinds"): what can be asked
        /// now, by kind, each with what it does and how much as its tip; a word with the peoples it would help less under
        /// "Others"; what can't be asked now under "Not now", each with why. <paramref name="prefix"/>: words before each label
        /// (whose debt it is, face to face). Used for every purpose's asks (a truce's price, a pact's cart) as well as a debt's.
        /// </summary>
        public static List<FloatMenuOption> Menu(List<Option> asks, Action<Option> pick, string prefix = null)
        {
            var list = new List<FloatMenuOption>();
            if (asks == null || asks.Count == 0) return list;
            FavourMath.MenuOrder(asks.Select(o => (o.group, o.why == null, o.more)).ToList(), out var shown, out var others, out var notNow);
            string Label(Option o) => prefix.NullOrEmpty() ? o.label : prefix + ": " + o.label;
            FloatMenuOption Can(Option o)
            {
                var m = new FloatMenuOption(Label(o), () => pick(o));
                if (!o.what.NullOrEmpty()) m.tooltip = new TipSignal(o.what);
                return m;
            }
            FloatMenuOption Not(Option o)
            {
                var m = new FloatMenuOption("CC_Out_Disabled".Translate(Label(o).Named("LABEL"), (o.why ?? "").Named("WHY")).Resolve(), null);
                if (!o.what.NullOrEmpty()) m.tooltip = new TipSignal(o.what);
                return m;
            }
            list.AddRange(shown.Select(i => Can(asks[i])));
            if (others.Count > 0)
            {
                var more = others.Select(i => asks[i]).ToList();
                list.Add(new FloatMenuOption(DipWords.P("CC_Dip_Menu_Others", more.Count.Named("COUNT")), () => Find.WindowStack.Add(new FloatMenu(more.Select(Can).ToList()))));
            }
            if (notNow.Count > 0)
            {
                var not = notNow.Select(i => asks[i]).ToList();
                list.Add(new FloatMenuOption(DipWords.P("CC_Dip_Menu_NotNow", not.Count.Named("COUNT")), () => Find.WindowStack.Add(new FloatMenu(not.Select(Not).ToList()))));
            }
            return list;
        }

        /// <summary>Why their fighters can't be asked for now: no threat on the home, a band already on its land, no fighters of theirs to send.</summary>
        public static string FightersWhy(Faction f, Map home)
        {
            if (home == null) return DipWords.P("CC_Dip_Why_NoHome");
            if (!MapFacts.ThreatOn(home)) return PactWords.P("CC_Pact_Why_NoThreat");
            if (CampTracker.WarBandAt(home) != null) return PactWords.P("CC_Pact_Why_BandHere");
            if (f?.def?.pawnGroupMakers == null || !f.def.pawnGroupMakers.Any(m => m.kindDef == PawnGroupKindDefOf.Combat))
                return DipWords.P("CC_Dip_Why_NoFighters", DipWords.People(f).Named("PEOPLE"));
            return null;
        }

        /// <summary>A word's weight in words: "a good word" for a small one, "a warm word" from 6 goodwill, "a strong word" from 9.</summary>
        private static string WordWords(int worth)
        {
            int gw = FavourMath.WordGoodwill(worth);
            return DipWords.P(gw >= 9 ? "CC_Dip_Word_Strong" : gw >= 6 ? "CC_Dip_Word_Warm" : "CC_Dip_Word_Good");
        }

        /// <summary>
        /// The goods a kind means for them, and how many make the load (the debt's worth by their nearest town's fortune and
        /// their means): medicine of their own age (Things/ThingRoles.cs: a mod's as well as the game's); food as a staple load
        /// of their age (plain meals or food that keeps, else grain or roots: never a handful of a dear dish); what a trader of
        /// theirs deals in (a material their caravans carry, in loads); wood, steel, silver. The same kind of a tie picks the
        /// same goods each time it's asked (its words and its load).
        /// </summary>
        public static (ThingDef def, int count) Goods(string kind, Faction f, Tie t)
        {
            ThingDef def;
            int seed = ThingRoles.Seed("favour goods", kind, f, t?.id ?? 0);
            float worth = FavourMath.GoodsWorth(Worth(t), Prosperity(f, t), FavourMath.Means(Ages.Of(f)));
            switch (kind)
            {
                case "medicine":
                    def = ThingRoles.From(ThingRole.Medicine, f, null, seed);
                    break;
                case "wood": def = ThingDefOf.WoodLog; break;
                case "steel": def = ThingDefOf.Steel; break;
                case "silver": def = ThingDefOf.Silver; break;
                case "trade": def = TradeGoods(f, seed) ?? ThingDefOf.Silver; break;
                default:
                    def = StapleFood(f, worth, seed);
                    break;
            }
            if (def == null) return (null, 0);
            return (def, FavourMath.GoodsCount(worth, def.BaseMarketValue));
        }

        /// <summary>
        /// Food as a staple load of their age (FavourMath.Staple): plain meals or food that keeps, else grain, roots or fruit
        /// from the fields, from the full pool of the player's mods (Things/ThingRoles.cs); pemmican when nothing fits.
        /// </summary>
        private static ThingDef StapleFood(Faction f, float worth, int seed)
        {
            bool Fits(ThingDef d)
            {
                if (d?.ingestible == null) return false;
                bool fine = d.ingestible.preferability >= FoodPreferability.MealFine;
                return FavourMath.Staple(fine, d.BaseMarketValue, d.GetStatValueAbstract(StatDefOf.Nutrition), worth);
            }
            return ThingRoles.From(ThingRole.Provisions, f, null, seed, Fits) ?? ThingRoles.From(ThingRole.Produce, f, null, seed, Fits) ?? ThingDefOf.Pemmican;
        }

        private static readonly Dictionary<FactionDef, List<ThingDef>> tradeGoods = new Dictionary<FactionDef, List<ThingDef>>();

        /// <summary>
        /// What a trader of theirs deals in: a material their caravans carry (cloth, leather, metal, blocks and the like, from
        /// the player's mods as well as the game), in loads and cheap a unit (FavourMath.TradeGood); never silver, a weapon,
        /// clothes, a drug or a dish. Null when their caravans carry none.
        /// </summary>
        private static ThingDef TradeGoods(Faction f, int seed)
        {
            var fd = f?.def;
            if (fd == null) return null;
            if (!tradeGoods.TryGetValue(fd, out var list))
            {
                list = new List<ThingDef>();
                try
                {
                    var gens = (fd.caravanTraderKinds ?? new List<TraderKindDef>()).Where(k => k?.stockGenerators != null).SelectMany(k => k.stockGenerators).Where(g => g != null).ToList();
                    if (gens.Count > 0)
                        foreach (var d in DefDatabase<ThingDef>.AllDefsListForReading)
                        {
                            if (d == null || d.category != ThingCategory.Item || !d.IsStuff || d == ThingDefOf.Silver || d.IsWeapon || d.IsApparel || d.IsDrug || d.IsIngestible) continue;
                            if (!d.tradeability.TraderCanSell() || !FavourMath.TradeGood(d.BaseMarketValue, d.stackLimit)) continue;
                            if (gens.Any(g => SafeHandles(g, d))) list.Add(d);
                        }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("favours: a trader's goods", e);
                }
                tradeGoods[fd] = list;
            }
            if (list.Count == 0) return null;
            Rand.PushState(seed);
            try
            {
                return list.RandomElement();
            }
            finally
            {
                Rand.PopState();
            }
        }

        private static bool SafeHandles(StockGenerator g, ThingDef d)
        {
            try
            {
                return g.HandlesThingDef(d);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Their nearest town's fortune (the notable's own town for a personal debt), 50 when the region keeps none.</summary>
        private static float Prosperity(Faction f, Tie t)
        {
            try
            {
                var n = FavoursOwed.NotableOf(t);
                var town = n != null ? NotableBook.TownOf(n.town) : WorldSituationWorker.NearestTown(f, FavoursOwed.HomeOf(t));
                var rec = town != null ? WorldLedger.TownRecord(town.ID) : null;
                return rec != null && rec.Seeded ? rec.prosperity : 50f;
            }
            catch
            {
                return 50f;
            }
        }

        /// <summary>Peoples they are on good terms with (not at war with them), whom the colony isn't at war with either.</summary>
        public static IEnumerable<Faction> ThirdPeoples(Faction f)
        {
            if (f == null) yield break;
            foreach (var o in Find.FactionManager.AllFactionsListForReading)
                if (o != f && Diplomacy.Deals(o) && o.HasGoodwill && !Diplomacy.Hostile(o) && !o.HostileTo(f))
                    yield return o;
        }

        /// <summary>Peoples at war with the colony that they still speak with (not at war with them).</summary>
        public static IEnumerable<Faction> Enemies(Faction f)
        {
            if (f == null) yield break;
            foreach (var o in Find.FactionManager.AllFactionsListForReading)
                if (o != f && Diplomacy.Deals(o) && o.HasGoodwill && Diplomacy.Hostile(o) && !o.HostileTo(f))
                    yield return o;
        }

        /// <summary>The colony's own people they hold (carried off, and not yet brought home).</summary>
        public static List<Pawn> Held(Faction f)
        {
            var list = new List<Pawn>();
            try
            {
                var k = f?.kidnapped?.KidnappedPawnsListForReading;
                if (k != null)
                    foreach (var p in k)
                        if (p != null && !p.Dead && p.RaceProps.Humanlike)
                            list.Add(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favours: who they hold", e);
            }
            return list;
        }

        /// <summary>Their open promise to speak for peace with that people, or null.</summary>
        public static Tie PeacePromised(Faction f, Faction enemy)
        {
            if (f == null || enemy == null) return null;
            var from = Refs.Of(f);
            var about = Refs.Of(enemy);
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Promise))
                if (t != null && t.Live && t.origin == FavourMath.Origin && t.from == from && t.terms.key == FavourMath.PeaceKey && t.terms.about == about)
                    return t;
            return null;
        }

        /// <summary>How well whoever answers for the debt thinks of the colony: the notable who owes (of the colonist owed), else their head, else the people's stance.</summary>
        public static int Regard(Tie t)
        {
            var f = FavoursOwed.PeopleOf(t);
            var n = FavoursOwed.NotableOf(t);
            if (n != null)
            {
                var owed = WorldLedger.PawnOf(t.to);
                return owed != null ? NotableBook.OpinionOf(n, owed) : NotableBook.Opinion(n);
            }
            var head = HeadOf(f, FavoursOwed.HomeOf(t));
            if (head != null) return NotableBook.Opinion(head);
            return f != null && f.HasGoodwill ? f.PlayerGoodwill : 0;
        }

        /// <summary>Their head the colony knows: of the town nearest it, else their people's leader's.</summary>
        public static Notable HeadOf(Faction f, Map home)
        {
            var book = NotableBook.Instance;
            if (f == null || book == null) return null;
            var town = home != null ? WorldSituationWorker.NearestTown(f, home) : null;
            return book.notables.Where(n => n != null && n.faction == f && n.role == NotableRole.Head && n.Alive && !Vassals.OutpostHead(n))
                .OrderBy(n => town != null && n.town == town.ID ? 0 : 1).FirstOrDefault();
        }

        // ================= The answer =================

        /// <summary>How they answered: honoured or refused, and their words for it (for their letter, or a message face to face).</summary>
        public sealed class Answer
        {
            public bool honoured;
            public string line;
            public List<Thing> things = new List<Thing>();
        }

        /// <summary>
        /// They hear the ask (their letter is read, or one of theirs hears it face to face) and answer it. Honoured: it is
        /// done (goods are in <see cref="Answer.things"/> for the letter's messenger to bring, or brought by their porters
        /// when <paramref name="byLetter"/> is false). Refused: the deed "brokeWord". <paramref name="answerDays"/>: by
        /// letter, the days its answer takes to come back (-1 a call or face to face): what is done far away waits for the
        /// answer to reach the colony (a word with another people, a captive let go, their help toward peace: AnswerIn), and
        /// their fighters walk in no sooner than it (the player, 11 November 2026: "It should always take the necessary time
        /// for stuff to actually get delivered, depending on the distance"). A captive let go otherwise walks the road home.
        /// </summary>
        public static Answer Hear(Tie t, string code, Map home, Pawn asker, bool byLetter, float roll, float answerDays = -1f)
        {
            var a = new Answer();
            var f = FavoursOwed.PeopleOf(t);
            if (t == null || f == null || !FavourMath.ParseAsk(code, out var ask, out string arg) || WhyNot(t) != null)
            {
                a.line = DipWords.T("CC_Dip_Ans_Moot");
                return a;
            }
            home ??= FavoursOwed.HomeOf(t);
            string who = FavoursOwed.NotableOf(t)?.Name ?? DipWords.People(f);
            FavoursOwed.CallIn(t, code);
            a.honoured = FavourMath.Honours(Regard(t), roll);
            if (!a.honoured)
            {
                FavoursOwed.Close(t, Tie.State.Broken);
                FactionMemory.Note(f, "brokeWord", DipWords.Ask(ask, arg));
                a.line = DipWords.T("CC_Dip_Ans_Refused", who.Named("WHO"), DipWords.People(f).Named("PEOPLE"), DipWords.Ask(ask, arg).Named("ASK"));
                return a;
            }
            // Done far away: it waits for its answer's road (by letter), or for the captive's walk home.
            bool letter = answerDays >= 0f;
            if (letter && FavourMath.DoneLater(ask) || ask == FavourAsk.Captive)
            {
                string later = Later(t, f, ask, arg, home, letter);
                if (later != null)
                {
                    a.line = DipWords.T("CC_Dip_Ans_Honoured", who.Named("WHO"), DipWords.People(f).Named("PEOPLE"), DipWords.Ask(ask, arg).Named("ASK")) + " " + later;
                    return a;
                }
                FavoursOwed.Close(t, Tie.State.Open);
                a.honoured = false;
                a.line = DipWords.T("CC_Dip_Ans_NotNow", DipWords.People(f).Named("PEOPLE"), DipWords.Ask(ask, arg).Named("ASK"));
                return a;
            }
            string done;
            try
            {
                done = Do(t, f, ask, arg, home, asker, byLetter, a.things, answerDays);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favours: honoured", e);
                done = null;
            }
            if (done == null && ask == FavourAsk.Fighters)
            {
                // Willing, but it couldn't be done now (no way onto the colony's land): the debt stands, open again.
                FavoursOwed.Close(t, Tie.State.Open);
                a.honoured = false;
                a.line = DipWords.T("CC_Dip_Ans_NotNow", DipWords.People(f).Named("PEOPLE"), DipWords.Ask(ask, arg).Named("ASK"));
                return a;
            }
            // Their fighters are kept when the band walks in (Pacts.cs); goods and the rest are done now.
            if (ask != FavourAsk.Fighters) FavoursOwed.Close(t, Tie.State.Kept);
            a.line = DipWords.T("CC_Dip_Ans_Honoured", who.Named("WHO"), DipWords.People(f).Named("PEOPLE"), DipWords.Ask(ask, arg).Named("ASK"))
                     + (done.NullOrEmpty() ? "" : " " + done);
            return a;
        }

        /// <summary>
        /// Honoured, and done far away: the debt stays called in, due when its answer is in (by letter: AnswerIn, or at
        /// <see cref="FavourMath.LetterDeedFallbackDays"/> if that answer never comes) or when the captive's walk home is done
        /// (the road from their town); the hour does it then (<see cref="Hourly"/>). Returns a line for their answer, or null
        /// when it can't be done (the captive no longer theirs to let go, the other people gone).
        /// </summary>
        private static string Later(Tie t, Faction f, FavourAsk ask, string arg, Map home, bool letter)
        {
            float now = Now / (float)FavourMath.DayTicks;
            switch (ask)
            {
                case FavourAsk.Captive:
                {
                    var p = DipWords.PawnOf(arg);
                    if (p == null || p.Dead || home == null || !Held(f).Contains(p)) return null;
                    t.terms.days = now + (letter ? FavourMath.LetterDeedFallbackDays : FavourMath.DeedDays(RoadDays(f, home)));
                    WorldLedger.Touch(t);
                    return DipWords.P(letter ? "CC_Dip_Done_Captive" : "CC_Dip_Done_CaptiveRoad", p.Named("PAWN"));
                }
                case FavourAsk.Word:
                {
                    var other = DipWords.FactionOf(arg);
                    if (other == null) return null;
                    t.terms.days = now + FavourMath.LetterDeedFallbackDays;
                    WorldLedger.Touch(t);
                    return DipWords.P("CC_Dip_Done_Word", DipWords.People(other).Named("PEOPLE"));
                }
                default:
                {
                    var enemy = DipWords.FactionOf(arg);
                    if (enemy == null) return null;
                    t.terms.days = now + FavourMath.LetterDeedFallbackDays;
                    WorldLedger.Touch(t);
                    return DipWords.P("CC_Dip_Done_Peace", DipWords.People(enemy).Named("PEOPLE"));
                }
            }
        }

        /// <summary>Days of the road from their nearest town to the colony's home, by their people's means.</summary>
        private static float RoadDays(Faction f, Map home)
        {
            var town = home != null ? WorldSituationWorker.NearestTown(f, home) : null;
            return LetterMath.LegDays(town != null ? WorldSituationWorker.Distance(home, town) : 20f, LetterMath.MeansOf(Ages.Of(f)));
        }

        /// <summary>A debt of theirs honoured and waiting for its road or its answer: a word, a captive let go, their help toward peace.</summary>
        private static bool Waits(Tie t, out FavourAsk ask, out string arg)
        {
            ask = FavourAsk.Goods;
            arg = null;
            return t != null && t.state == Tie.State.CalledIn && !FavoursOwed.ColonyOwes(t) && FavourMath.ParseAsk(t.terms.goods, out ask, out arg) && FavourMath.DoneLater(ask)
                   && t.terms.days > 0f;
        }

        /// <summary>
        /// A letter's answer is in (NeighbourLetters.Owed): a debt it called in and they honoured with a word, a captive let go or
        /// their help toward peace is done now, with the answer that says so. A call's waits for the road (Hourly).
        /// </summary>
        public static void AnswerIn(NeighbourLetter l)
        {
            if (l == null || l.purpose != LetterPurpose.Favour || l.channel == LetterChannel.Console || l.topicKey == null) return;
            if (!l.topicKey.StartsWith(DiplomacyLetters.TiePrefix, StringComparison.Ordinal) || !int.TryParse(l.topicKey.Substring(DiplomacyLetters.TiePrefix.Length), out int id)) return;
            var t = FavoursOwed.Get(id);
            if (!Waits(t, out _, out _)) return;
            try
            {
                Keep(t, Now / (float)FavourMath.DayTicks);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favours: done with the answer", e);
            }
        }

        /// <summary>The hour: debts honoured far away whose road is done (or whose letter's answer never came) are done now.</summary>
        public static void Hourly(int now)
        {
            float day = now / (float)FavourMath.DayTicks;
            List<Tie> due = null;
            foreach (var t in FavoursOwed.Live())
                if (Waits(t, out _, out _) && day >= t.terms.days)
                    (due ??= new List<Tie>()).Add(t);
            if (due == null) return;
            foreach (var t in due) Keep(t, day);
        }

        /// <summary>
        /// Done now: the word put in, the captive set down at the edge of the colony's land to walk home, the promise of peace;
        /// the debt kept. A captive who can't be set down now waits an hour; one no longer theirs, the debt stands again.
        /// </summary>
        private static void Keep(Tie t, float day)
        {
            if (!Waits(t, out var ask, out string arg)) return;
            var f = FavoursOwed.PeopleOf(t);
            var home = FavoursOwed.HomeOf(t) ?? HappeningTracker.HomeMap();
            if (f == null || home == null || Diplomacy.Hostile(f))
            {
                FavoursOwed.Close(t, Tie.State.Ended);
                return;
            }
            if (ask == FavourAsk.Captive)
            {
                var p = DipWords.PawnOf(arg);
                if (p == null || p.Dead || !Held(f).Contains(p))
                {
                    FavoursOwed.Close(t, Tie.State.Open);
                    return;
                }
                if (!WalkHome(f, p, home))
                {
                    t.terms.days = day + 1f / 24f;
                    return;
                }
            }
            else if (ask == FavourAsk.Word)
            {
                var other = DipWords.FactionOf(arg);
                if (other != null) PutInWord(f, other, home, FavourMath.WordGoodwill(Worth(t)));
            }
            else
            {
                var enemy = DipWords.FactionOf(arg);
                if (enemy != null) PromisePeace(f, enemy, home);
            }
            FavoursOwed.Close(t, Tie.State.Kept);
        }

        /// <summary>What honouring it does now; returns a line for their answer about what follows on the map.</summary>
        private static string Do(Tie t, Faction f, FavourAsk ask, string arg, Map home, Pawn asker, bool byLetter, List<Thing> things, float answerDays = -1f)
        {
            switch (ask)
            {
                case FavourAsk.Goods:
                {
                    var (def, count) = Goods(arg, f, t);
                    if (def == null || count <= 0) return null;
                    var made = Couriers.Make(def, count);
                    string goods = Couriers.Describe(made);
                    if (byLetter)
                    {
                        // Their messenger brings it with the answer (NeighbourLetters: the answer's courier sets it down).
                        things.AddRange(made);
                        return DipWords.P("CC_Dip_Done_GoodsLetter", goods.Named("GOODS"));
                    }
                    if (home == null) return null;
                    // Their porters bring it some hours later, by day, and set it down by the gate (Couriers/); nobody to
                    // send, it is set down the old way.
                    Couriers.Loot(made, home, DropCellFinder.TradeDropSpot(home), f);
                    return DipWords.P("CC_Dip_Done_Goods", goods.Named("GOODS"), DipWords.People(f).Named("PEOPLE"));
                }
                case FavourAsk.Word:
                {
                    var other = DipWords.FactionOf(arg);
                    if (other == null) return null;
                    PutInWord(f, other, home, FavourMath.WordGoodwill(Worth(t)));
                    return DipWords.P("CC_Dip_Done_Word", DipWords.People(other).Named("PEOPLE"));
                }
                case FavourAsk.Fighters:
                {
                    var at = int.TryParse(arg, out int key) ? Homes.MapOf(key) ?? home : home;
                    if (at == null) return null;
                    // Their band sets out now and walks in some hours later (a letter's answer adds its walk back), never
                    // before the answer that says they are coming.
                    var town = WorldSituationWorker.NearestTown(f, at);
                    float hours = PactMath.BandHours(town != null ? WorldSituationWorker.Distance(at, town) : 6f) + (byLetter ? 12f : 0f);
                    if (answerDays > 0f) hours = Mathf.Max(hours, answerDays * 24f);
                    t.terms.days = Find.TickManager.TicksGame / (float)FavourMath.DayTicks + hours / 24f;
                    WorldLedger.Touch(t);
                    return DipWords.P("CC_Dip_Done_Fighters", DipWords.People(f).Named("PEOPLE"));
                }
                default:
                {
                    var enemy = DipWords.FactionOf(arg);
                    if (enemy == null) return null;
                    PromisePeace(f, enemy, home);
                    return DipWords.P("CC_Dip_Done_Peace", DipWords.People(enemy).Named("PEOPLE"));
                }
            }
        }

        /// <summary>
        /// Their word with a third people: that people thinks better of the colony (half the debt's size in goodwill), and says
        /// so in a letter of its own; the history keeps it (dip:word).
        /// </summary>
        public static void PutInWord(Faction f, Faction other, Map home, int goodwill)
        {
            WorldSituationWorker.Goodwill(other, Mathf.Max(1, goodwill), false);
            string text = DipWords.T("CC_Dip_WordLetter", DipWords.People(other).Named("PEOPLE"), DipWords.People(f).Named("FRIEND"));
            string label = DipWords.P("CC_Dip_WordLetter_Label", DipWords.People(other).Named("PEOPLE"));
            // Their letter comes by the road from their nearest town, in the post (Post/MailIn.cs); the post off, told now.
            var town = home != null ? WorldSituationWorker.NearestTown(other, home) : null;
            if (!MailIn.FromAbroad(home, other, town, label, text, LetterDefOf.PositiveEvent))
                Tell.Letter(NewsKind.World, Importance.Notable, home, label, text, LetterDefOf.PositiveEvent, LookTargets.Invalid, other);
            Diplomacy.Write(DiplomacyMath.Word, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(Diplomacy.Colony(home), HistoryRole.For), new HistoryActor(Refs.Of(other), HistoryRole.With)
            }, Diplomacy.Colony(home), DipWords.P("CC_Dip_Hist_Word", DipWords.People(f).Named("FRIEND"), DipWords.People(other).Named("PEOPLE")));
        }

        /// <summary>
        /// Their help toward peace: a Promise from them to the colony, about the people at war with it (key "peace", 20 in
        /// its number), standing a year. The truce's answer (release 2a.2) reads it as that people thinking 20 better of the
        /// colony; until then it is kept and shown.
        /// </summary>
        public static void PromisePeace(Faction f, Faction enemy, Map home)
        {
            var colony = Diplomacy.Colony(home);
            var terms = new TieTerms { key = FavourMath.PeaceKey, number = FavourMath.PeaceRegard, about = Refs.Of(enemy) };
            WorldLedger.AddTie(Tie.Kind.Promise, Refs.Of(f), colony, terms, Now + (int)(FavourMath.PeaceDays * FavourMath.DayTicks), FavourMath.Origin);
            Diplomacy.Write(DiplomacyMath.Peace, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(colony, HistoryRole.For), new HistoryActor(Refs.Of(enemy), HistoryRole.About)
            }, colony, DipWords.P("CC_Dip_Hist_Peace", DipWords.People(f).Named("FRIEND"), DipWords.People(enemy).Named("PEOPLE")), true);
        }

        /// <summary>
        /// A colonist they hold, let go: taken off their list of captives and set down at the edge of the colony's land,
        /// from where they walk to the gate (the game's own way back for a ransomed colonist of a people of the old ways).
        /// </summary>
        public static bool WalkHome(Faction f, Pawn p, Map home)
        {
            try
            {
                if (p == null || p.Dead || home == null || f?.kidnapped == null) return false;
                if (!CellFinder.TryFindRandomEdgeCellWith(c => home.reachability.CanReachColony(c) && !c.Fogged(home), home, CellFinder.EdgeRoadChance_Neutral, out var cell))
                    return false;
                f.kidnapped.RemoveKidnappedPawn(p);
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                GenSpawn.Spawn(p, cell, home);
                if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
                var gate = DropCellFinder.TradeDropSpot(home);
                if (gate.IsValid && p.jobs != null)
                {
                    var job = JobMaker.MakeJob(JobDefOf.Goto, gate);
                    job.locomotionUrgency = Pace.Errand;
                    p.jobs.StartJob(job, JobCondition.InterruptForced);
                }
                Diplomacy.Write(DiplomacyMath.Freed, new List<HistoryActor>
                {
                    new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(WorldLedger.PersonOf(p), HistoryRole.For)
                }, Diplomacy.Colony(home), DipWords.P("CC_Dip_Hist_Freed", DipWords.People(f).Named("PEOPLE"), p.Named("PAWN")), true);
                // apart: the colonist is set down at the edge of the colony's land and walks in; the message looks at them.
                Tell.Message(NewsKind.World, Importance.Important, home, DipWords.P("CC_Dip_Msg_Freed", p.Named("PAWN"), DipWords.People(f).Named("PEOPLE")),
                    new LookTargets(p), MessageTypeDefOf.PositiveEvent);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favours: a colonist walked home", e);
                return false;
            }
        }
    }
}
