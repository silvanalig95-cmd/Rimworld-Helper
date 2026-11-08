using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The letter purpose Dealing (World/NeighbourLetters.cs, through Diplomacy/DiplomacyLetters.cs): a dealing with a town's
    /// council or a people's leaders, written by a colonist and carried as every letter is (the mailbag and whoever goes
    /// their way, the colony's rider, a relay, a chain of neighbours, a call), read by the town's head for the council, who
    /// answers by their regard and their people's ways (DealingMath.Chance); and the colony's answer to a letter the world
    /// wrote first (Dealings/WorldWrites.cs). The letter keeps what it is in its topic ("deal:Supply:0:1234": the kind, its
    /// sub-kind and the town; "reply:12": the world's letter it answers) and the terms in its help field ("terms:Corn:75",
    /// "reply:yes"). The purpose is offered in the letter's window only when chosen for it (from "Do something with..." on
    /// Peoples, or a town's or a people's card).
    /// </summary>
    public static class DealingLetters
    {
        public const string TermsPrefix = "terms:", ReplyPrefix = "reply:";

        public static bool Owns(LetterPurpose p) => p == LetterPurpose.Dealing;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static string W(string key, params NamedArgument[] args) => Dealings.W(key, args);

        // ================= What a letter to them can be about =================

        /// <summary>Why nothing can be written to them as a dealing now (no dealing open with their town or people, no letter of theirs to answer), or null.</summary>
        public static string WhyNot(Notable n, Map home)
        {
            if (n == null) return W("CC_Deal_Why_NoOne", "?".Named("PEOPLE"), "?".Named("TOWN"));
            if (!Dealings.On) return Dealings.Why("Off");
            if (Topics(n, home).Count > 0) return null;
            var town = NotableBook.TownOf(n.town);
            if (town != null)
            {
                Dealings.FirstOpen(DealKind.Supply, n.faction, town, home, out string why);
                if (why != null) return why;
            }
            return W("CC_Deal_Why_Nothing", (n.faction?.Name ?? "?").Named("PEOPLE"));
        }

        /// <summary>
        /// What a dealing letter to this notable can be about: an answer to a letter of theirs that waits for one; the
        /// dealings open with their town when they receive for its council; the people's as a whole when they're its head.
        /// </summary>
        public static List<LetterTopic> Topics(Notable n, Map home)
        {
            var list = new List<LetterTopic>();
            if (n?.faction == null || !Dealings.On) return list;
            try
            {
                foreach (var a in WorldWrites.Waiting(n))
                    list.Add(new LetterTopic { key = ReplyPrefix + a.id, text = W("CC_Deal_Topic_Reply_" + a.kind, n.Name.Named("NAME")) });
                var f = n.faction;
                var town = NotableBook.TownOf(n.town);
                bool forTown = town != null && town.Faction == f && Dealings.Receiver(f, town, home) == n;
                bool forPeople = Dealings.Receiver(f, null, home) == n;
                foreach (var k in DealingMath.Kinds)
                    for (byte s = 0; s < DealingMath.Subs(k); s++)
                    {
                        if (forTown && Dealings.WhyNot(k, s, f, town, home) == null)
                            list.Add(new LetterTopic { key = DealingMath.TopicKey(k, s, town.ID), text = Topic(k, s, f, town) });
                        if (forPeople && !DealingMath.TownOnly(k) && Dealings.WhyNot(k, s, f, null, home) == null)
                            list.Add(new LetterTopic { key = DealingMath.TopicKey(k, s, -1), text = Topic(k, s, f, null) });
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: a letter's topics", e);
            }
            return list;
        }

        private static string Topic(DealKind k, byte s, Faction f, Settlement town) =>
            W("CC_Deal_Topic", Dealings.Label(k, s).Named("DEAL"), Dealings.Council(f, town).Named("COUNCIL"));

        /// <summary>To whom the letter is addressed in its words: the town's council, or the people's leaders; a reply to the one who wrote.</summary>
        public static string ToName(Notable n, string topicKey)
        {
            if (n == null || topicKey == null || topicKey.StartsWith(ReplyPrefix, StringComparison.Ordinal)) return null;
            if (!DealingMath.ParseTopic(topicKey, out _, out _, out int townId)) return null;
            return Dealings.Council(n.faction, townId >= 0 ? NotableBook.TownOf(townId) : null);
        }

        // ================= What is asked =================

        /// <summary>The terms a dealing can be asked on, each greyed with why not; none for those whose topic is the ask (a proclamation, a petition, a town invited).</summary>
        public static List<FavourCalls.Option> Asks(string topicKey, Map home)
        {
            var list = new List<FavourCalls.Option>();
            if (topicKey == null) return list;
            if (topicKey.StartsWith(ReplyPrefix, StringComparison.Ordinal)) return WorldWrites.Asks(topicKey, home);
            if (!DealingMath.ParseTopic(topicKey, out var k, out byte s, out int townId)) return list;
            var town = townId >= 0 ? NotableBook.TownOf(townId) : null;
            var f = town?.Faction;
            try
            {
                switch (k)
                {
                    case DealKind.Supply:
                        if (s == (byte)SupplyWay.Ours)
                            foreach (var (def, count) in Pacts.CartGoods(home))
                                list.Add(new FavourCalls.Option
                                {
                                    code = TermsPrefix + def.defName + ":" + count,
                                    label = W("CC_Deal_Ask_OurLoad", Couriers.Amount(count, null, def).Named("GOODS"), PactMath.CartPay(def.BaseMarketValue, count).Named("SILVER"))
                                });
                        else if (f != null) list.AddRange(Dealings.TheirGoods(f, home));
                        break;
                    case DealKind.Relief:
                        foreach (var (def, count) in Dealings.ReliefGoods(home))
                            list.Add(new FavourCalls.Option { code = TermsPrefix + def.defName + ":" + count, label = W("CC_Deal_Ask_Relief", Couriers.Amount(count, null, def).Named("GOODS")) });
                        break;
                    case DealKind.Work:
                    {
                        var (what, def, count) = Dealings.WorkShare((WorkKind)s, home, town);
                        if (def != null)
                            list.Add(new FavourCalls.Option
                            {
                                code = TermsPrefix + def.defName + ":" + count, label = W("CC_Deal_Ask_Share", Couriers.Amount(count, null, def).Named("GOODS")),
                                why = Dealings.WorkShareWhyNot((WorkKind)s, home, town)
                            });
                        break;
                    }
                    case DealKind.Market:
                    {
                        int fee = DealingMath.MarketFee(Ages.Of(f));
                        list.Add(new FavourCalls.Option
                        {
                            code = TermsPrefix + "Silver:" + fee, label = W("CC_Deal_Ask_Fee", fee.Named("SILVER"), DealingMath.MarketOffset.ToStringPercent().Named("TERMS")),
                            why = Dealings.Silver(home) < fee ? W("CC_Deal_Why_PayShort", fee.Named("SILVER")) : null
                        });
                        break;
                    }
                    case DealKind.Hire:
                    {
                        int wage = Dealings.HireWage(f, home, (HireKind)s);
                        list.Add(new FavourCalls.Option
                        {
                            code = TermsPrefix + "Silver:" + wage, label = W("CC_Deal_Ask_Wage", wage.Named("SILVER")),
                            why = Dealings.Silver(home) < wage ? W("CC_Deal_Why_PayShort", wage.Named("SILVER")) : null
                        });
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: the terms", e);
            }
            return list;
        }

        public static bool ParseTerms(string code, out string defName, out int count)
        {
            defName = null;
            count = 0;
            if (code == null || !code.StartsWith(TermsPrefix, StringComparison.Ordinal)) return false;
            var parts = code.Substring(TermsPrefix.Length).Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out count)) return false;
            defName = parts[0];
            return true;
        }

        // ================= Their answer =================

        /// <summary>
        /// Their council has read it (or heard the call): agreed or refused by their regard and their ways, and the answer's
        /// tone follows (refused is a refusal; agreed, never worse than cool). The dealing is written in the book now, its
        /// answer on its way: what it brings starts when the answer is in (<see cref="AnswerIn"/>). Amends come with the
        /// answer itself, in silver. A dealing overtaken by events is answered without a yes or a no.
        /// </summary>
        public static string Read(NeighbourLetter l, Notable n, Map home, ref AnswerTone tone, List<Thing> things)
        {
            if (l?.topicKey == null || n?.faction == null) return null;
            if (l.topicKey.StartsWith(ReplyPrefix, StringComparison.Ordinal)) return WorldWrites.Read(l, n, home, ref tone);
            if (!DealingMath.ParseTopic(l.topicKey, out var k, out byte s, out int townId)) return null;
            var f = n.faction;
            var town = townId >= 0 ? NotableBook.TownOf(townId) : null;
            string council = Dealings.Council(f, town);
            // Overtaken: the town no longer theirs, the setting off, a war where only petitions are heard.
            bool petition = k == DealKind.Petition;
            if (!Dealings.On || townId >= 0 && (town == null || town.Faction != f) || Diplomacy.Hostile(f) && !petition)
            {
                DiplomacyLetters.Moot(l);
                if (tone < AnswerTone.Cool) tone = AnswerTone.Cool;
                return W("CC_Deal_Ans_Moot", council.Named("COUNCIL"));
            }
            if (tone == AnswerTone.Silence) return null;
            var ways = new DealWays { calling = Peoples.CallingOf(f), age = Ages.Of(f), empire = Ages.IsEmpire(f) };
            int regard = NotableBook.Opinion(n), goodwill = f.HasGoodwill ? f.PlayerGoodwill : 0;
            bool yes = DealingDebug.AllAgree || DealingMath.Agrees(k, s, regard, goodwill, ways, Rand.Value);
            ParseTerms(l.help, out string defName, out int count);
            var book = DealingBook.Instance;
            var d = new Dealing
            {
                id = book != null ? book.nextId++ : 0, kind = k, sub = s, people = f, town = townId, townName = town?.Name ?? f.Name, home = l.map ?? home, letter = l.id,
                notable = n.id, stage = DealStage.Asked, asked = Now, yes = yes, goods = defName, count = count
            };
            var def = defName != null ? DefDatabase<ThingDef>.GetNamedSilentFail(defName) : null;
            // The silver of a supply contract each season, by the goods and how many.
            if (k == DealKind.Supply && def != null)
                d.silver = s == (byte)SupplyWay.Ours ? PactMath.CartPay(def.BaseMarketValue, count) : DealingMath.TheirLoadPay(def.BaseMarketValue, count);
            if (k == DealKind.Market || k == DealKind.Hire) d.silver = count;
            string goods = def != null ? Couriers.Amount(count, null, def) : "";
            // Amends: the silver comes with their answer, set down at the gate by whoever brings it.
            int amends = 0;
            if (yes && petition && s == (byte)PetitionKind.Reparations)
            {
                amends = DealingMath.Reparations(Dealings.WrongWeight(f), ways.age);
                things.AddRange(Couriers.Make(ThingDefOf.Silver, amends));
                d.silver = amends;
            }
            book?.deals.Add(d);
            Dealings.Changed();
            if (!yes) tone = AnswerTone.Refusal;
            else if (tone > AnswerTone.Cool) tone = AnswerTone.Cool;
            string key = DealingMath.AnswerKey(k, s, yes);
            return W(key, council.Named("COUNCIL"), (town?.Name ?? f.Name ?? "?").Named("TOWN"), (f.Name ?? "?").Named("PEOPLE"), goods.Named("GOODS"),
                (amends > 0 ? amends : d.silver).Named("SILVER"), (Dealings.Council(f, town)).Named("THEY"));
        }

        /// <summary>The answer is in at the colony (set down at the mailbag, brought by the rider, the call ended): agreed, it starts now; refused, it stands refused.</summary>
        public static void AnswerIn(NeighbourLetter l)
        {
            if (l == null || !Owns(l.purpose)) return;
            var d = DealingBook.Instance?.ByLetter(l.id);
            if (d == null || d.stage != DealStage.Asked) return;
            try
            {
                if (d.yes)
                {
                    d.stage = DealStage.Agreed;
                    Dealings.Begin(d);
                }
                else
                {
                    d.stage = DealStage.Refused;
                    Dealings.Changed();
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: the answer in", e);
            }
        }

        /// <summary>The answer's title: the council answers (a reply's, the one who wrote).</summary>
        public static string Title(NeighbourLetter l, Notable n)
        {
            if (l?.topicKey == null || n == null || l.topicKey.StartsWith(ReplyPrefix, StringComparison.Ordinal)) return null;
            if (!DealingMath.ParseTopic(l.topicKey, out _, out _, out int townId)) return null;
            return W("CC_Deal_Ans_Title", Dealings.Council(n.faction, townId >= 0 ? NotableBook.TownOf(townId) : null).CapitalizeFirst().Named("COUNCIL"));
        }
    }
}
