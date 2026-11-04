using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Stage 2's letter purposes, in the existing machinery (World/NeighbourLetters.cs: the writer at a table or the caller
    /// at the console, their messenger at the gate, the answer by tone): this class says whether a purpose can be written,
    /// what it can be about and what is asked, and what their answer does; NeighbourLetters asks it for the purposes it
    /// owns. Favour (a debt of honour called in, or the colony's word on their side when they called one in); Pact (a defence
    /// or a trade pact proposed, or one ended by the colony's letter without blame) and Truce (offered to a people at war,
    /// its term and price) in release 2a.2; Match, Ward, Ransom, Envoy and Fealty come with their releases
    /// (DiplomacyMath.Purposes).
    /// <para>The letter keeps what it is about in its topic ("tie:12" a debt of theirs, "side:15" the colony's word on a
    /// call of theirs, "pact:trade:7" a pact proposed to people 7, "pactend:9" pact 9 ended, "truce:year:7" a truce offered)
    /// and what is asked in its help field (FavourMath.AskCode; a truce's price "price:Ours:200"; a trade pact's cart
    /// "cart:Corn:75").</para>
    /// </summary>
    public static class DiplomacyLetters
    {
        public const string TiePrefix = "tie:", SidePrefix = "side:", PactPrefix = "pact:", PactEndPrefix = "pactend:", TrucePrefix = "truce:", PricePrefix = "price:",
            CartPrefix = "cart:";

        public static string TieKey(Tie t) => TiePrefix + t.id;

        public static string SideKey(Tie t) => SidePrefix + t.id;

        /// <summary>A purpose this frame answers for.</summary>
        public static bool Owns(LetterPurpose p) => p == LetterPurpose.Favour || p == LetterPurpose.Pact || p == LetterPurpose.Truce;

        private static Map Home => HappeningTracker.HomeMap() ?? Homes.Seat;

        private static Tie TieOf(string key, string prefix) =>
            key != null && key.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(key.Substring(prefix.Length), out int id) ? FavoursOwed.Get(id) : null;

        // ================= Can it be written =================

        /// <summary>Why it can't be written to them now, or null (the general rules, one at a time and the wait, come first in NeighbourLetters).</summary>
        public static string WhyNot(Notable n, LetterPurpose p)
        {
            if (!Owns(p) || n == null) return null;
            if (p == LetterPurpose.Truce) return Truces.WhyNot(n.faction, Home);
            if (p == LetterPurpose.Pact)
            {
                if (Topics(n, p).Count > 0) return null;
                return Pacts.WhyNotAny(n.faction, Home) ?? PactWords.P("CC_Pact_Why_Nothing", PactWords.People(n.faction).Named("PEOPLE"));
            }
            if (!Diplomacy.FavoursOn) return DipWords.P("CC_Dip_Why_Off");
            if (NotableBook.Hostile(n)) return DipWords.P("CC_Dip_Why_War", DipWords.People(n.faction).Named("PEOPLE"));
            return Topics(n).Count == 0 ? DipWords.P("CC_Dip_Why_NothingOwed", DipWords.People(n.faction).Named("PEOPLE")) : null;
        }

        /// <summary>
        /// What a letter to them can be about: a debt their people owe the colony (to the head or anyone of theirs), one they
        /// owe in person (to them only), and a call of theirs on the colony's word that waits for the colony's answer.
        /// </summary>
        public static List<LetterTopic> Topics(Notable n, LetterPurpose p)
        {
            if (p == LetterPurpose.Favour) return Topics(n);
            var list = new List<LetterTopic>();
            if (n?.faction == null) return list;
            var f = n.faction;
            var home = Home;
            try
            {
                if (p == LetterPurpose.Truce)
                {
                    if (Truces.WhyNot(f, home) != null) return list;
                    foreach (TruceTerm term in Enum.GetValues(typeof(TruceTerm)))
                        list.Add(new LetterTopic { key = TrucePrefix + PactMath.TermKey(term, false) + ":" + f.loadID, text = PactWords.P("CC_Truce_Topic", PactWords.Term(term).Named("TERM")) });
                }
                else if (p == LetterPurpose.Pact)
                {
                    foreach (PactKind k in Enum.GetValues(typeof(PactKind)))
                        if (Pacts.WhyNot(f, home, k) == null)
                            list.Add(new LetterTopic { key = PactPrefix + PactMath.KindKey(k) + ":" + f.loadID, text = PactWords.P("CC_Pact_Topic", PactWords.Kind(k).Named("KIND")) });
                    // A pact of the colony's with them ended by its own letter, without blame.
                    foreach (var t in Pacts.With(f))
                        if (Pacts.HomeOf(t) == home && !Pacts.CallUnderWay(t))
                            list.Add(new LetterTopic { key = PactEndPrefix + t.id, text = PactWords.P("CC_Pact_TopicEnd", PactWords.Kind(Pacts.KindOf(t)).Named("KIND")) });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("diplomacy letters: pact and truce topics", e);
            }
            return list;
        }

        /// <summary>A debt of honour's topics (the purpose Favour).</summary>
        public static List<LetterTopic> Topics(Notable n)
        {
            var list = new List<LetterTopic>();
            if (n?.faction == null) return list;
            try
            {
                foreach (var t in FavoursOwed.OwedBy(n.faction))
                {
                    if (FavourCalls.WhyNot(t) != null) continue;
                    if (FavoursOwed.IsPersonal(t) && t.from != n.Person) continue;
                    list.Add(new LetterTopic { key = TieKey(t), text = DipWords.DebtLine(t) });
                }
                foreach (var t in FavourAsks.Waiting())
                    if (Refs.Faction(t.to) == n.faction && FavourMath.ParseNeed(t.terms.goods, out var need) && need == FavourNeed.Dispute)
                        list.Add(new LetterTopic { key = SideKey(t), text = DipWords.P("CC_Dip_Topic_Side", DipWords.People(n.faction).Named("PEOPLE")) });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("diplomacy letters: topics", e);
            }
            return list;
        }

        private static string asksFor;
        private static float asksAt = -1f;
        private static List<FavourCalls.Option> asks = new List<FavourCalls.Option>();

        /// <summary>What can be asked in a letter about this topic, each with why not (none for the colony's word: the letter is the answer). Worked out at most twice a second.</summary>
        public static List<FavourCalls.Option> Asks(string topicKey, Map map)
        {
            if (asksFor == topicKey && Time.realtimeSinceStartup - asksAt < 0.5f) return asks;
            if (topicKey != null && topicKey.StartsWith(TrucePrefix, StringComparison.Ordinal)) asks = TruceAsks(topicKey, map);
            else if (topicKey != null && topicKey.StartsWith(PactPrefix, StringComparison.Ordinal)) asks = CartAsks(topicKey, map);
            else
            {
                var t = TieOf(topicKey, TiePrefix);
                asks = t == null ? new List<FavourCalls.Option>() : FavourCalls.Options(t, map);
            }
            asksFor = topicKey;
            asksAt = Time.realtimeSinceStartup;
            return asks;
        }

        /// <summary>A truce's price, either way, each greyed with why not: none, the colony's silver, theirs, their captives returned.</summary>
        private static List<FavourCalls.Option> TruceAsks(string topicKey, Map map)
        {
            var list = new List<FavourCalls.Option>();
            if (!ParseTopic(topicKey, TrucePrefix, out string what, out var f)) return list;
            map ??= Home;
            int silver = PactMath.PriceSilver(StorytellerUtility.DefaultThreatPointsNow(map ?? Find.AnyPlayerHomeMap));
            int captives = Truces.Captives(f, map).Count;
            foreach (TrucePrice price in Enum.GetValues(typeof(TrucePrice)))
            {
                int amount = price == TrucePrice.Ours || price == TrucePrice.Theirs ? silver : price == TrucePrice.Captives ? captives : 0;
                string why = null;
                if (price == TrucePrice.Ours && PlayCarry.Have(map, "silver", ThingDefOf.Silver) < silver)
                    why = PactWords.P("CC_Truce_Why_NoSilver", silver.Named("SILVER"));
                if (price == TrucePrice.Captives && captives == 0) why = PactWords.P("CC_Truce_Why_NoCaptives", PactWords.People(f).Named("PEOPLE"));
                list.Add(new FavourCalls.Option { code = PricePrefix + price + ":" + amount, label = PactWords.Price(price, amount).CapitalizeFirst(), why = why });
            }
            return list;
        }

        /// <summary>A trade pact's cart, from the colony's surplus; nothing to ask in a defence pact.</summary>
        private static List<FavourCalls.Option> CartAsks(string topicKey, Map map)
        {
            var list = new List<FavourCalls.Option>();
            if (!ParseTopic(topicKey, PactPrefix, out string what, out _) || what != PactMath.Trade) return list;
            foreach (var (def, count) in Pacts.CartGoods(map ?? Home))
                list.Add(new FavourCalls.Option { code = CartPrefix + def.defName + ":" + count, label = PactWords.P("CC_Pact_CartOption", Couriers.Amount(count, null, def).Named("GOODS")) });
            return list;
        }

        /// <summary>"truce:year:7" or "pact:trade:7": what it is (the term, the kind) and the people.</summary>
        private static bool ParseTopic(string key, string prefix, out string what, out Faction f)
        {
            what = null;
            f = null;
            if (key == null || !key.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var parts = key.Substring(prefix.Length).Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int id)) return false;
            what = parts[0];
            f = Refs.Faction(Ref.People(id));
            return f != null;
        }

        private static bool ParseCode(string code, string prefix, out string a, out int n)
        {
            a = null;
            n = 0;
            if (code == null || !code.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var parts = code.Substring(prefix.Length).Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out n)) return false;
            a = parts[0];
            return true;
        }

        // ================= The dialog's own rows =================

        /// <summary>
        /// The letter dialog's rows for a purpose of this frame, under "What for": what about, and what is asked. Returns the
        /// height used; <paramref name="why"/> says why it can't be sent as it stands (nothing that can be asked).
        /// </summary>
        public static float DrawRows(Rect inRect, float y, List<LetterTopic> topics, ref int topicIndex, ref string ask, Map map, out string why)
        {
            why = null;
            float start = y;
            if (topics.Count == 0) return 0f;
            topicIndex = Mathf.Clamp(topicIndex, 0, topics.Count - 1);
            var topic = topics[topicIndex];
            Widgets.Label(new Rect(inRect.x, y + 2f, 120f, 24f), "CC_LetterDlg_About".Translate());
            if (Widgets.ButtonText(new Rect(inRect.x + 124f, y, inRect.width - 124f, 26f), topic.text.Truncate(inRect.width - 140f), true, true, topics.Count > 1))
            {
                var opts = new List<FloatMenuOption>();
                for (int i = 0; i < topics.Count; i++)
                {
                    int k = i;
                    var me = topics;
                    opts.Add(new FloatMenuOption(me[i].text, () => Picked = k));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            TooltipHandler.TipRegion(new Rect(inRect.x + 124f, y, inRect.width - 124f, 26f), topic.text);
            if (Picked >= 0)
            {
                topicIndex = Picked;
                Picked = -1;
                ask = null;
            }
            y += 30f;
            var asks = Asks(topic.key, map);
            if (asks.Count == 0) return y - start;
            // What is asked: the first that can be, unless one is chosen.
            string now = ask;
            if (now == null || !asks.Any(o => o.Code == now && o.why == null)) now = asks.FirstOrDefault(o => o.why == null)?.Code;
            ask = now;
            var chosen = asks.FirstOrDefault(o => o.Code == now);
            Widgets.Label(new Rect(inRect.x, y + 2f, 120f, 24f), "CC_LetterDlg_Ask".Translate());
            string shown = chosen?.label ?? DipWords.P("CC_Dip_Dlg_NothingToAsk");
            if (Widgets.ButtonText(new Rect(inRect.x + 124f, y, inRect.width - 124f, 26f), shown.Truncate(inRect.width - 140f)))
                Find.WindowStack.Add(new FloatMenu(asks.Select(o => o.why == null
                    ? new FloatMenuOption(o.label, () => Asked = o.Code)
                    : new FloatMenuOption("CC_Out_Disabled".Translate(o.label.Named("LABEL"), o.why.Named("WHY")).Resolve(), null)).ToList()));
            if (Asked != null)
            {
                ask = Asked;
                Asked = null;
            }
            if (chosen == null) why = DipWords.P("CC_Dip_Dlg_NothingToAsk");
            y += 30f;
            return y - start;
        }

        // A pick from a float menu, taken up on the next frame (the menu closes after the dialog has drawn).
        private static int Picked = -1;
        private static string Asked;

        // ================= Their answer =================

        /// <summary>What their answer does, and its words for their letter.</summary>
        public sealed class Answer
        {
            public string line;
            public List<Thing> things = new List<Thing>();
        }

        /// <summary>
        /// They have read it (NeighbourLetters.Read): a debt called in is honoured or refused (by their regard, not the
        /// letter's luck), and the answer's tone follows (a refusal is a refusal; honoured, never worse than cool); the
        /// colony's word on their side is taken with thanks. Null for a purpose this frame doesn't own.
        /// </summary>
        public static Answer Read(NeighbourLetter l, Notable n, Pawn speaker, ref AnswerTone tone)
        {
            if (l == null || !Owns(l.purpose)) return null;
            var a = new Answer();
            try
            {
                if (l.purpose == LetterPurpose.Truce || l.purpose == LetterPurpose.Pact)
                {
                    PactAnswer(l, n, ref tone, a);
                    return a;
                }
                var side = TieOf(l.topicKey, SidePrefix);
                if (side != null)
                {
                    if (side.state == Tie.State.CalledIn) FavourAsks.SideTaken(side, KinMemory.Ours(speaker) ? speaker : null);
                    if (tone > AnswerTone.Cordial) tone = AnswerTone.Cordial;
                    a.line = DipWords.T("CC_Dip_Ans_Side", n.Name.Named("NAME"), DipWords.People(n.faction).Named("PEOPLE"));
                    return a;
                }
                var t = TieOf(l.topicKey, TiePrefix);
                if (t == null)
                {
                    a.line = DipWords.T("CC_Dip_Ans_Moot");
                    return a;
                }
                var heard = FavourCalls.Hear(t, l.help, l.map, KinMemory.Ours(speaker) ? speaker : null, true, Rand.Value);
                a.line = heard.line;
                a.things.AddRange(heard.things);
                if (!heard.honoured) tone = AnswerTone.Refusal;
                else if (tone > AnswerTone.Cool) tone = AnswerTone.Cool;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("diplomacy letters: the answer", e);
            }
            return a;
        }

        /// <summary>
        /// A truce offered or a pact proposed, read (or heard): sworn or refused by their own reckoning (Truces.Hear,
        /// Pacts.Hear), and the answer's tone follows it (refused is a refusal, never silence; agreed, never worse than cool).
        /// A pact ended by the colony's letter is taken without blame.
        /// </summary>
        private static void PactAnswer(NeighbourLetter l, Notable n, ref AnswerTone tone, Answer a)
        {
            var f = n.faction;
            var home = l.map ?? Home;
            bool yes;
            if (l.purpose == LetterPurpose.Truce)
            {
                if (!ParseTopic(l.topicKey, TrucePrefix, out string term, out var tf) || tf != f)
                {
                    a.line = PactWords.T("CC_Truce_Ans_Moot", PactWords.People(f).Named("PEOPLE"));
                    tone = AnswerTone.Cool;
                    return;
                }
                PactMath.ParseTerm(term, out var t, out _);
                ParseCode(l.help, PricePrefix, out string priceKey, out int amount);
                var price = PactMath.ParsePrice(priceKey);
                // Their envoy comes after their answer's walk back (a call: some hours).
                float envoy = l.channel == LetterChannel.Console ? Rand.Range(PactMath.EnvoyHoursMin, PactMath.EnvoyHoursMax) / 24f : l.backDays + 0.5f;
                a.line = Truces.Hear(f, home, t, price, amount, envoy, Rand.Value, out yes);
            }
            else if (l.topicKey != null && l.topicKey.StartsWith(PactEndPrefix, StringComparison.Ordinal))
            {
                var t = int.TryParse(l.topicKey.Substring(PactEndPrefix.Length), out int id) ? Pacts.Get(id) : null;
                if (t != null && t.Live) Pacts.EndByLetter(t);
                a.line = PactWords.T("CC_Pact_Ans_Ended", PactWords.People(f).Named("PEOPLE"));
                if (tone < AnswerTone.Cool) tone = AnswerTone.Cool;
                return;
            }
            else
            {
                if (!ParseTopic(l.topicKey, PactPrefix, out string kind, out var pf) || pf != f || !PactMath.ParseKind(kind, out var k))
                {
                    a.line = PactWords.T("CC_Pact_Ans_Moot", PactWords.People(f).Named("PEOPLE"));
                    tone = AnswerTone.Cool;
                    return;
                }
                ThingDef cart = null;
                int count = 0;
                if (ParseCode(l.help, CartPrefix, out string defName, out int c))
                {
                    cart = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                    count = c;
                }
                a.line = Pacts.Hear(f, home, k, cart, count, Rand.Value, out yes);
            }
            if (!yes) tone = AnswerTone.Refusal;
            else if (tone > AnswerTone.Cool) tone = AnswerTone.Cool;
        }
    }
}
