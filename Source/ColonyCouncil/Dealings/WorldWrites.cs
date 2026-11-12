using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The world writes first (docs/design/post-and-letters.md, step 2, point 13): now and then one of the notables the colony
    /// knows, who thinks well enough of it, writes to it unasked, more often with pen friends (DealingMath.WriteChance: about
    /// once in two or three weeks, never two waiting at once, never within six days of the last). News of their town and
    /// region, a small request (medicine, wood, cloth or steel by their age), or an invitation to their town's festival.
    /// The letter travels as every letter does (in the post from their town, set down at the mailbag) and wants an answer:
    /// from their card, the inbox or "Do something with..." the colony writes back (the purpose Dealing, its topic
    /// "reply:ID"), and the answer leaves with the next carrier going their way. Read, it warms them (and counts toward a
    /// pen friend); a request granted brings their porters to the gate for it; an invitation accepted is the festival's
    /// journey (Dealings/DealingFestival.cs). Left unanswered half a month, they take the silence as it is.
    /// </summary>
    public static class WorldWrites
    {
        public static bool On => (ColonyCouncilMod.Settings?.worldWrites ?? true) && NeighbourLetters.On && NotableBook.On;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static DealingBook Book => DealingBook.Instance;

        private static string W(string key, params NamedArgument[] args) => Dealings.W(key, args);

        /// <summary>Their letters opened at the colony and waiting for its answer (none being written back already).</summary>
        public static List<WorldAsk> Waiting(Notable n)
        {
            var book = Book;
            if (book == null || n == null) return new List<WorldAsk>();
            return book.asks.Where(a => a.notable == n.id && a.opened >= 0 && !a.answered && Dealings.OpenLetter(DealingLetters.ReplyPrefix + a.id) == null).ToList();
        }

        public static List<WorldAsk> AllWaiting()
        {
            var book = Book;
            if (book == null) return new List<WorldAsk>();
            return book.asks.Where(a => a.opened >= 0 && !a.answered && Dealings.OpenLetter(DealingLetters.ReplyPrefix + a.id) == null && !Writing(a)).ToList();
        }

        /// <summary>
        /// A letter of the colony's to them is under way since their letter came (written, in the bag or on the road): their
        /// ask waits on it rather than on the colony, so it neither shows as waiting nor lapses as silence meanwhile (the player:
        /// "the "asks waiting" information is really nice, but it should disappear once you have an answer ordered").
        /// </summary>
        public static bool Writing(WorldAsk a) =>
            a != null && (NotableBook.Instance?.letters.Any(l => l != null && l.Open && l.notable == a.notable && l.created >= a.opened) ?? false);

        /// <summary>Their letter opened at the colony and waiting for its answer, or null.</summary>
        public static WorldAsk OpenAsk(Notable n) =>
            n == null ? null : Book?.asks.FirstOrDefault(a => a != null && a.notable == n.id && a.opened >= 0 && !a.answered);

        // ================= They write =================

        /// <summary>Who might write first: notables alive and at peace, not at the colony, who think this well of it or more, with a town known.</summary>
        private static List<Notable> Writers(Map home)
        {
            var nb = NotableBook.Instance;
            var book = Book;
            if (nb == null || book == null || home == null) return new List<Notable>();
            return nb.notables.Where(n => n != null && n.Alive && n.faction != null && !NotableBook.Hostile(n) && !n.pawn.Spawned && NotableBook.TownOf(n.town) != null
                                          && NotableBook.Opinion(n) >= DealingMath.WriterRegard && !book.asks.Any(a => a.notable == n.id && !a.answered)).ToList();
        }

        public static void Daily(int now)
        {
            var book = Book;
            if (!On || book == null) return;
            var home = HappeningTracker.HomeMap() ?? Homes.Seat;
            if (home == null) return;
            var writers = Writers(home);
            int waiting = book.asks.Count(a => !a.answered);
            float since = book.lastWrote < 0 ? -1f : (now - book.lastWrote) / (float)DealingMath.DayTicks;
            float chance = DealingMath.WriteChance(writers.Count, PenFriends.Count, waiting, since) * StoryDirection.Factor(StoryWaysMath.Letters);
            if (!Rand.Chance(chance)) return;
            // Pen friends write more often than anyone else.
            var n = writers.RandomElementByWeightWithFallback(x => PenFriends.Is(x) ? 3f : 1f);
            if (n != null) Write(n, home, now);
        }

        /// <summary>A letter from them to the colony, on its way in the post (or, the post off, told now).</summary>
        public static WorldAsk Write(Notable n, Map home, int now, ReplyKind? kind = null)
        {
            var book = Book;
            var town = NotableBook.TownOf(n?.town ?? -1);
            if (book == null || n?.faction == null || town == null || home == null) return null;
            var f = n.faction;
            bool canInvite = DealingFestival.CanOffer(f, town, home);
            var k = kind ?? DealingMath.WriteKind(Rand.Value, canInvite);
            if (k == ReplyKind.Invitation && !canInvite) k = ReplyKind.Request;
            var a = new WorldAsk { id = book.nextId++, notable = n.id, people = f, town = town.ID, kind = k, written = now, home = home };
            string body;
            switch (k)
            {
                case ReplyKind.Request:
                    a.goods = DealingMath.RequestGoods(Ages.Of(f), Rand.Value);
                    a.count = DealingMath.RequestCount(a.goods);
                    body = W("CC_World_Text_Request", n.Name.Named("NAME"), town.Name.Named("TOWN"), (f.Name ?? "?").Named("PEOPLE"), GoodsWord(a).Named("GOODS"));
                    break;
                case ReplyKind.Invitation:
                    body = W("CC_World_Text_Invitation", n.Name.Named("NAME"), town.Name.Named("TOWN"), (f.Name ?? "?").Named("PEOPLE"));
                    break;
                default:
                    var facts = Facts.For(FactScene.Letter(n, null, home, true));
                    var news = facts?.news?.Take(2).ToList() ?? new List<string>();
                    body = W("CC_World_Text_News", n.Name.Named("NAME"), town.Name.Named("TOWN"), (f.Name ?? "?").Named("PEOPLE"))
                           + (news.Count > 0 ? "\n" + string.Join("\n", news.Select(x => "  - " + x)) : "\n  - " + W("CC_World_Text_NewsQuiet", town.Name.Named("TOWN"), n.Name.Named("NAME")));
                    break;
            }
            // Written out in full (Letters/LetterForm.cs): their own letter, by what they want of it, and the lines above under
            // it as the narrator's (what they ask, their news, how to answer).
            var full = LetterForm.Full ? LetterForm.First(n, k, k == ReplyKind.Request ? GoodsWord(a) : null,
                new List<string> { body, W("CC_World_HowToAnswer", n.Name.Named("NAME")) }, a.id) : null;
            if (full != null) body = full.full;
            else
            {
                if (PenFriends.Is(n)) body += "\n\n" + W("CC_World_Text_Friend", n.Name.Named("NAME"));
                body += "\n\n" + W("CC_World_HowToAnswer", n.Name.Named("NAME"));
            }
            string label = W("CC_World_Label_" + k, n.Name.Named("NAME"));
            string bring = W("CC_World_Bring", n.Name.Named("NAME"));
            a.postId = MailIn.FromAbroadId(home, f, town, label, body, LetterDefOf.NeutralEvent, bring);
            if (a.postId < 0)
            {
                Tell.Letter(NewsKind.World, Importance.Notable, home, label, body, LetterDefOf.NeutralEvent, LookTargets.Invalid, f);
                a.opened = now;
            }
            book.asks.Add(a);
            book.lastWrote = now;
            Dealings.Changed();
            return a;
        }

        public static string GoodsWord(WorldAsk a)
        {
            var def = GoodsDef(a, null);
            return def != null ? Couriers.Amount(a.count, null, def) : Couriers.Amount(a.count, a.goods, null);
        }

        /// <summary>The thing a request names: the colony's own medicine (whatever kind it keeps most of), wood, or the def it names.</summary>
        public static ThingDef GoodsDef(WorldAsk a, Map home)
        {
            if (a?.goods == null) return null;
            if (a.goods == "medicine")
            {
                var best = home?.resourceCounter?.AllCountedAmounts.Where(kv => kv.Key != null && kv.Key.IsMedicine && kv.Value >= a.count).OrderBy(kv => kv.Key.BaseMarketValue)
                    .Select(kv => kv.Key).FirstOrDefault();
                return best ?? DefDatabase<ThingDef>.GetNamedSilentFail("MedicineHerbal");
            }
            return a.goods == "wood" ? ThingDefOf.WoodLog : DefDatabase<ThingDef>.GetNamedSilentFail(a.goods);
        }

        /// <summary>The hour: a letter on its way opens when it is set down at the mailbag; one left unanswered too long is taken as silence.</summary>
        public static void Hourly(int now)
        {
            var book = Book;
            if (book == null || book.asks.Count == 0) return;
            var post = PostBook.Instance;
            foreach (var a in book.asks)
            {
                if (a.opened < 0)
                {
                    if (a.postId < 0 || post == null || post.Item(a.postId) == null)
                    {
                        a.opened = now;
                        a.postId = -1;
                        Dealings.Changed();
                    }
                    continue;
                }
                if (a.answered || now - a.opened < DealingMath.AnswerDays * DealingMath.DayTicks || Dealings.OpenLetter(DealingLetters.ReplyPrefix + a.id) != null || Writing(a)) continue;
                a.answered = true;
                // The colony wrote to them since in its own way (not the answer they asked for): no silence to take.
                if (NotableBook.Instance?.letters.Any(l => l != null && l.notable == a.notable && l.created >= a.opened && l.stage != LetterStage.Cancelled) ?? false)
                {
                    Dealings.Changed();
                    continue;
                }
                var n = NotableBook.Instance?.Get(a.notable);
                if (n != null)
                {
                    n.personal += DealingMath.UnansweredRegard;
                    n.Remember(W("CC_World_Note_Unanswered"), DealingMath.UnansweredRegard);
                }
                Dealings.Changed();
            }
        }

        // ================= The colony's answer =================

        /// <summary>What the colony's answer says, each greyed with why not.</summary>
        public static List<FavourCalls.Option> Asks(string topicKey, Map home)
        {
            var list = new List<FavourCalls.Option>();
            var a = AskOf(topicKey);
            if (a == null) return list;
            switch (a.kind)
            {
                case ReplyKind.Request:
                    var def = GoodsDef(a, home);
                    bool have = def != null && home != null && PlayCarry.Have(home, def.defName, def) >= a.count;
                    list.Add(new FavourCalls.Option
                    {
                        code = DealingLetters.ReplyPrefix + "yes", label = W("CC_World_Ask_Send", (def != null ? Couriers.Amount(a.count, null, def) : GoodsWord(a)).Named("GOODS")),
                        why = have ? null : W("CC_World_Why_Short", (def != null ? Couriers.Amount(a.count, null, def) : GoodsWord(a)).Named("GOODS"))
                    });
                    list.Add(new FavourCalls.Option { code = DealingLetters.ReplyPrefix + "no", label = W("CC_World_Ask_Decline") });
                    break;
                case ReplyKind.Invitation:
                    list.Add(new FavourCalls.Option { code = DealingLetters.ReplyPrefix + "yes", label = W("CC_World_Ask_Accept") });
                    list.Add(new FavourCalls.Option { code = DealingLetters.ReplyPrefix + "no", label = W("CC_World_Ask_DeclineInvite") });
                    break;
                default:
                    list.Add(new FavourCalls.Option { code = DealingLetters.ReplyPrefix + "yes", label = W("CC_World_Ask_Thanks") });
                    break;
            }
            return list;
        }

        public static WorldAsk AskOf(string topicKey) =>
            topicKey != null && topicKey.StartsWith(DealingLetters.ReplyPrefix, StringComparison.Ordinal) && int.TryParse(topicKey.Substring(DealingLetters.ReplyPrefix.Length), out int id)
                ? Book?.Ask(id) : null;

        /// <summary>
        /// They read the colony's answer: warmed by it (a request granted more), the answer's tone never worse than cool and,
        /// granted, courteous at least; what follows starts when their thanks are in (<see cref="Begin"/>).
        /// </summary>
        public static string Read(NeighbourLetter l, Notable n, Map home, ref AnswerTone tone)
        {
            var a = AskOf(l?.topicKey);
            if (a == null || n == null)
            {
                DiplomacyLetters.Moot(l);
                return W("CC_World_Ans_Moot", (n?.Name ?? "?").Named("NAME"));
            }
            a.answered = true;
            bool yes = l.help == DealingLetters.ReplyPrefix + "yes";
            n.personal += yes && a.kind == ReplyKind.Request ? DealingMath.GrantedRegard : DealingMath.ReplyRegard;
            if (tone == AnswerTone.Silence || tone == AnswerTone.Refusal || tone == AnswerTone.Curt) tone = AnswerTone.Cool;
            if (yes && tone > AnswerTone.Cordial) tone = AnswerTone.Cordial;
            var book = Book;
            if (book != null)
                book.deals.Add(new Dealing
                {
                    id = book.nextId++, kind = DealKind.Reply, sub = (byte)a.kind, people = n.faction, town = a.town, townName = NotableBook.TownOf(a.town)?.Name ?? n.townName,
                    home = l.map ?? home, letter = l.id, notable = n.id, stage = DealStage.Asked, asked = Now, yes = yes, askId = a.id, goods = a.goods, count = a.count
                });
            Dealings.Changed();
            return W("CC_World_Ans_" + a.kind + (yes ? "_Yes" : "_No"), n.Name.Named("NAME"), GoodsWord(a).Named("GOODS"));
        }

        /// <summary>Their thanks are in: a request granted, their porters come to the gate for it; an invitation accepted, the festival's journey is offered.</summary>
        public static void Begin(Dealing d, Map home)
        {
            var a = Book?.Ask(d.askId);
            if (!d.yes || a == null)
            {
                Dealings.End(d, DealStage.Kept);
                return;
            }
            if (a.kind == ReplyKind.Request)
            {
                var def = GoodsDef(a, home);
                d.goods = def?.defName;
                if (def == null)
                {
                    Dealings.End(d, DealStage.Ended);
                    return;
                }
                var trip = Couriers.CollectOrder(home, def, a.count, 0, d.people);
                if (trip != null)
                {
                    d.tripId = trip.id;
                    return;
                }
                if (VarietyUtil.TakeFromStores(home, def, a.count)) Granted(d);
                else Dealings.End(d, DealStage.Ended);
                return;
            }
            if (a.kind == ReplyKind.Invitation)
            {
                if (!DealingFestival.Invite(d, home))
                {
                    Dealings.End(d, DealStage.Ended, W("CC_Deal_Festival_NoMission", (d.townName ?? "?").Named("TOWN")));
                    return;
                }
            }
            Dealings.End(d, DealStage.Kept);
        }

        /// <summary>What they asked for is with them: a favor done, and their people remember it.</summary>
        public static void Granted(Dealing d)
        {
            if (d.people != null) FactionMemory.Note(d.people, "favorDone", d.townName);
            Dealings.End(d, DealStage.Kept);
        }
    }
}
