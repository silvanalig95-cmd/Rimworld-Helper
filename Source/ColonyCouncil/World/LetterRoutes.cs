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
    /// Whom a letter goes to (the rules are World/LetterAddressMath.cs): a personal letter to anyone, a town's matter to its
    /// head, a people's matter to their leader if the colony knows them, else to the nearest town's head. A town's matter
    /// written to someone else is passed on by them (<see cref="Pass"/>): slower by the extra leg to the head's town, or a day
    /// within the same town, with their good word or their poor one; the head answers it, and their answer says who brought
    /// it. The letter's window, the town's card, "Write" on the Who of a card and the people's page ask <see cref="WhyNot"/>,
    /// so they grey a letter for the same reason. Nothing here is saved but the letter's own <c>passTo</c> and <c>passed</c>.
    /// </summary>
    public static class LetterRoutes
    {
        private static NotableBook Book => NotableBook.Instance;

        private static Map Home => HappeningTracker.HomeMap() ?? Homes.Seat;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= Who speaks for whom =================

        /// <summary>The head of a town the colony knows by name, alive, or null.</summary>
        public static Notable HeadOf(int town) =>
            town < 0 ? null : Book?.notables.FirstOrDefault(n => n != null && n.town == town && n.role == NotableRole.Head && n.Alive);

        /// <summary>Their people's own leader, known to the colony by name, or null.</summary>
        public static Notable LeaderOf(Faction f) =>
            f?.leader == null || f.leader.Dead ? null : Book?.notables.FirstOrDefault(n => n != null && n.faction == f && n.Alive && n.pawn == f.leader);

        /// <summary>Who speaks for their people as a whole: their leader if the colony knows them, else the head of their town nearest it (FavourCalls.HeadOf).</summary>
        public static Notable SpeakerOf(Faction f, Map home = null) => LeaderOf(f) ?? FavourCalls.HeadOf(f, home ?? Home);

        /// <summary>What decides whom a letter to them goes to.</summary>
        public static AddressFacts FactsOf(Notable n, Map home = null)
        {
            if (n == null) return default;
            home ??= Home;
            var f = n.faction;
            var leader = LeaderOf(f);
            var nearest = leader == null ? FavourCalls.HeadOf(f, home) : null;
            bool receiver = false;
            try
            {
                if (Dealings.On && f != null)
                {
                    var town = NotableBook.TownOf(n.town);
                    receiver = town != null && town.Faction == f && Dealings.Receiver(f, town, home) == n || Dealings.Receiver(f, null, home) == n;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letter routes: a council's receiver", e);
            }
            return new AddressFacts
            {
                role = n.role, leader = leader == n, townHead = HeadOf(n.town) != null || n.role == NotableRole.Head, leaderKnown = leader != null,
                nearestHead = nearest == n, speakerKnown = leader != null || nearest != null, receiver = receiver
            };
        }

        /// <summary>Whose matter a letter of this purpose about this topic is (a debt they owe in person, an answer to their letter: theirs).</summary>
        public static LetterScope ScopeOf(LetterPurpose p, string topicKey, Notable n = null) =>
            LetterAddressMath.ScopeOf(p, LetterAddressMath.PersonalTopic(p, topicKey, PersonalDebt(topicKey)));

        /// <summary>A debt's topic ("tie:12") that someone owes in person.</summary>
        private static bool PersonalDebt(string topicKey)
        {
            if (topicKey == null || !topicKey.StartsWith(DiplomacyLetters.TiePrefix, StringComparison.Ordinal)) return false;
            return int.TryParse(topicKey.Substring(DiplomacyLetters.TiePrefix.Length), out int id) && FavoursOwed.IsPersonal(FavoursOwed.Get(id));
        }

        /// <summary>Who answers a letter of this purpose and scope written to them: they, their town's head, whoever speaks for their people; null when nobody the colony knows does.</summary>
        public static Notable Answerer(Notable n, LetterPurpose p, LetterScope scope, Map home = null)
        {
            Way(n, p, scope, out var who, home);
            return who;
        }

        /// <summary>How a letter of this purpose and scope written to them reaches the one who answers it, and who that is (null with nobody known).</summary>
        public static LetterWay Way(Notable n, LetterPurpose p, LetterScope scope, out Notable who, Map home = null)
        {
            who = null;
            if (n == null) return LetterWay.NoHead;
            home ??= Home;
            var a = LetterAddressMath.WhoAnswers(p, scope, FactsOf(n, home));
            switch (a)
            {
                case LetterAnswerer.Self: who = n; break;
                case LetterAnswerer.TownHead: who = HeadOf(n.town); break;
                case LetterAnswerer.Speaker: who = SpeakerOf(n.faction, home); break;
            }
            if (who == null) a = LetterAnswerer.None;
            else if (who == n) a = LetterAnswerer.Self;
            return LetterAddressMath.Way(p, a);
        }

        // ================= What can be written =================

        /// <summary>What a letter of this purpose and scope to them can be about: the one who answers its topics, only those of its scope.</summary>
        public static List<LetterTopic> Topics(Notable n, LetterPurpose p, LetterScope scope)
        {
            var who = Answerer(n, p, scope) ?? n;
            var all = NeighbourLetters.Topics(who, p);
            if (!LetterAddressMath.Mixed(p)) return all;
            return all.Where(t => ScopeOf(p, t.key, who) == scope).ToList();
        }

        /// <summary>
        /// Why a letter of this purpose can't be written to them now, whatever its scope (a debt or a dealing: either of
        /// theirs), or null. <paramref name="occasion"/>: a kind letter with no reason now is held too (World/LetterOccasions.cs).
        /// </summary>
        public static string WhyNot(Notable n, LetterPurpose p, bool occasion)
        {
            if (!LetterAddressMath.Mixed(p)) return WhyNot(n, p, LetterAddressMath.ScopeOf(p, false), occasion);
            string town = WhyNot(n, p, LetterScope.Town, occasion);
            if (town == null) return null;
            return WhyNot(n, p, LetterScope.Personal, occasion) == null ? null : town;
        }

        /// <summary>
        /// Why a letter of this purpose and scope can't be written to them now, or null: nobody known speaks for the town or
        /// their people, a council's dealing that only its head can take, a kind letter with no reason (with
        /// <paramref name="occasion"/>), the letters' own checks on them and on the one who answers it, and the purpose's own
        /// on the one who answers it.
        /// </summary>
        public static string WhyNot(Notable n, LetterPurpose p, LetterScope scope, bool occasion)
        {
            if (n == null || !n.Alive) return "CC_Letter_Why_Gone".Translate();
            var way = Way(n, p, scope, out var who);
            if (way == LetterWay.NoHead) return NoHeadWhy(n, scope);
            if (way == LetterWay.HeadOnly)
                return "CC_Route_Why_HeadOnly".Translate(Short(who).Named("HEAD"), (n.townName ?? n.faction?.Name ?? "?").Named("TOWN")).Resolve();
            if (occasion && NeighbourLetters.On)
            {
                string held = LetterOccasions.Held(who, p);
                if (held != null) return held;
            }
            string why = NeighbourLetters.GeneralWhyNot(n, p);
            if (why == null && who != n) why = HeadWhyNot(who, p);
            if (why != null) return why;
            why = NeighbourLetters.PurposeWhyNot(who, p);
            if (why == null && LetterAddressMath.Mixed(p) && Topics(n, p, scope).Count == 0)
                why = ("CC_Route_Why_Nothing_" + p + "_" + scope).Translate(n.Name.Named("NAME"), (n.faction?.Name ?? "?").Named("PEOPLE")).Resolve();
            return why;
        }

        /// <summary>The head a letter is passed on to: one at a time to them too, not here at the colony, and the wait for the same thing.</summary>
        private static string HeadWhyNot(Notable who, LetterPurpose p)
        {
            if (who == null || !who.Alive) return "CC_Letter_Why_Gone".Translate();
            // One at a time to the head too, while the colony still waits for the last one's answer: what it waits for, in words.
            var busy = NeighbourLetters.AwaitedFrom(who);
            if (busy != null) return "CC_Route_Why_HeadBusy".Translate(who.Name.Named("HEAD"), NeighbourLetters.WaitWords(busy).Named("WAIT")).Resolve();
            if (who.pawn.Spawned) return "CC_Letter_Why_Here".Translate(who.Name.Named("NAME")).Resolve();
            int opens = NeighbourLetters.OpensAgain(who, p);
            if (opens >= 0) return "CC_Route_Why_HeadLately".Translate(who.Name.Named("HEAD"), NeighbourLetters.DateOf(opens).Named("DATE")).Resolve();
            return null;
        }

        /// <summary>Nobody the colony knows speaks for their town (or their people), and how one comes to be known.</summary>
        public static string NoHeadWhy(Notable n, LetterScope scope) =>
            (scope == LetterScope.People ? "CC_Route_Why_NoSpeaker" : "CC_Route_Why_NoHead")
            .Translate((n?.townName ?? n?.faction?.Name ?? "?").Named("TOWN"), (n?.faction?.Name ?? "?").Named("PEOPLE")).Resolve();

        // ================= Passing it on =================

        /// <summary>
        /// The letter has reached the one it was written to (<paramref name="n"/>): true when they are to pass it on now, to
        /// <paramref name="who"/>. A letter written to the head, a personal letter, or one passed on already: false. An older
        /// save's letter to someone who doesn't speak for the town on a town's matter is passed on all the same, and a letter
        /// to a head who is no longer the head goes on to the one who is.
        /// </summary>
        public static bool Passes(NeighbourLetter l, Notable n, out Notable who)
        {
            who = null;
            if (l == null || n == null || l.passed || NotableMath.Retired(l.purpose)) return false;
            try
            {
                var scope = ScopeOf(l.purpose, l.topicKey, n);
                if (scope == LetterScope.Personal) return false;
                var chosen = l.passTo >= 0 ? Book?.Get(l.passTo) : null;
                if (chosen != null && chosen.Alive && chosen != n && chosen.faction == n.faction) who = chosen;
                else if (Way(n, l.purpose, scope, out var now, l.map) == LetterWay.PassOn) who = now;
                return who != null && who != n;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letter routes: passing it on", e);
                return false;
            }
        }

        /// <summary>
        /// They pass it on to <paramref name="who"/>: a letter goes on the road from their town to the head's by their people's
        /// means (a day within the same town), and the head reads it then; a call is passed on and the head calls back
        /// (<paramref name="callHours"/>: the hours until then, from the call). The answer comes back from the head's town.
        /// <paramref name="say"/>: told in a message (not when an aide took the call, which has its own).
        /// </summary>
        public static void Pass(NeighbourLetter l, Notable n, Notable who, int now, float callHours = -1f, bool say = true)
        {
            if (l == null || n == null || who == null) return;
            var from = NotableBook.TownOf(n.town);
            var to = NotableBook.TownOf(who.town);
            bool same = n.town == who.town || from == null || to == null;
            l.passTo = who.id;
            l.passed = true;
            l.stageTick = now;
            if (callHours >= 0f)
            {
                l.stage = LetterStage.CallbackWait;
                l.tries = 0;
                l.due = now + Mathf.RoundToInt(callHours * GenDate.TicksPerHour);
            }
            else
            {
                float leg = same ? 0f : LetterMath.LegDays(LivingEvents.Distance(from.Tile, to.Tile), LetterMath.MeansOf(Ages.Of(n.faction)));
                l.stage = LetterStage.Travelling;
                l.due = now + Mathf.RoundToInt((LetterAddressMath.PassDays(same, leg) + NotableMath.ReplyDays(Rand.Value)) * GenDate.TicksPerDay);
                // The answer comes back from the head's town, by the colony's means.
                if (!same && l.map != null) l.backDays = LetterMath.LegDays(LivingEvents.Distance(l.map.Tile, to.Tile), NeighbourLetters.Means);
            }
            n.Remember("CC_Route_Note_Passed".Translate(l.WriterName.Named("PAWN"), who.Name.Named("HEAD")).Resolve(), 0f);
            NotableBook.Changed();
            // apart: a letter passed on at their town (a call: words they will pass on), told in a message.
            if (say)
                Notify.LetterMessage((callHours >= 0f ? "CC_Route_Msg_PassedCall" : "CC_Route_Msg_Passed").Translate(n.Name.Named("NAME"), l.WriterName.Named("PAWN"),
                    Short(who).Named("HEAD")).Resolve(), l.id, LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
        }

        /// <summary>The colony's own rider at the town of the one it was written to: they take it on to the head, and the answer comes from there in the post; the rider rides home.</summary>
        public static void PassFromRider(NeighbourLetter l, Notable n, Notable who, int now)
        {
            l.ownRider = false;
            l.rider = null;
            Pass(l, n, who, now);
        }

        /// <summary>Who reads it once it is in hand: the head it was passed on to, else the one it was written to.</summary>
        public static Notable Reader(NeighbourLetter l, Notable n)
        {
            if (l != null && l.passed && l.passTo >= 0)
            {
                var h = Book?.Get(l.passTo);
                if (h != null) return h;
            }
            return n;
        }

        /// <summary>The go-between's word as the head reads it (World/LetterAddressMath.cs, Word); 0 for a letter written to the one who answers it.</summary>
        public static float WordOf(NeighbourLetter l, Notable reader)
        {
            if (l == null || !l.passed || reader == null || l.notable == reader.id) return 0f;
            var via = Book?.Get(l.notable);
            return via == null ? 0f : LetterAddressMath.Word(via.role, NotableBook.Opinion(via));
        }

        /// <summary>
        /// The head's line in their answer that says who brought it ("Acraeraes brought me your letter."): a letter, words given
        /// by mouth, or a call passed on; in a medieval hand its own words. Null for a letter not passed on.
        /// </summary>
        public static string PassLine(NeighbourLetter l, Notable reader, bool olde)
        {
            if (l == null || !l.passed || reader == null || l.notable == reader.id) return null;
            var via = Book?.Get(l.notable);
            string name = via?.Name ?? "?";
            return LetterAddressMath.PassLineKey(l.channel == LetterChannel.Console, l.spoken, olde).Translate(name.Named("NAME"), l.WriterName.Named("PAWN")).Resolve();
        }

        /// <summary>The letter's addressee in the messages and the mailbag: "Golakath, reeve of Karond-Keth 2, by way of Acraeraes".</summary>
        public static string ToVia(Notable who, Notable via) =>
            "CC_Route_ToVia".Translate(NotableBook.Called(who).Named("HEAD"), (via?.Name ?? "?").Named("NAME")).Resolve();

        /// <summary>"Golakath, reeve": a name with what they are to their town.</summary>
        public static string Short(Notable n) => n == null ? "?" : n.Name + ", " + NotableBook.TitleOf(n);

        // ================= Words for the window =================

        /// <summary>
        /// Who they are and whether they speak for the town, for the letter's window: "Acraeraes of Karond-Keth 2, a trader who
        /// called: personal letters. For the town's matters, write to Golakath, reeve." <paramref name="other"/>: whom the
        /// window offers to write to instead (the town's head, or their people's leader), or null.
        /// </summary>
        public static string Header(Notable n, out Notable other, Map home = null)
        {
            other = null;
            if (n == null) return "";
            home ??= Home;
            var f = FactsOf(n, home);
            var town = LetterAddressMath.WhoAnswers(LetterPurpose.Warning, LetterScope.Town, f);
            var head = town == LetterAnswerer.TownHead ? HeadOf(n.town) : null;
            if (town == LetterAnswerer.TownHead && head == null) town = LetterAnswerer.None;
            string key = LetterAddressMath.HeaderKey(town, f.leader);
            string line = key.Translate(NotableBook.Called(n).Named("WHO"), (head != null ? Short(head) : "?").Named("HEAD"),
                (n.townName ?? n.faction?.Name ?? "?").Named("TOWN"), (n.faction?.Name ?? "?").Named("PEOPLE")).Resolve();
            other = head;
            // The whole people's matters go to someone else again: their leader, or the nearest town's head.
            if (!f.leader)
            {
                Way(n, LetterPurpose.Pact, LetterScope.People, out var speaker, home);
                if (speaker != null && speaker != n && speaker != head)
                {
                    line += " " + "CC_Route_Header_PeopleTo".Translate(Short(speaker).Named("SPEAKER"), (n.faction?.Name ?? "?").Named("PEOPLE")).Resolve();
                    other ??= speaker;
                }
            }
            return line;
        }

        /// <summary>The route under the purposes for a letter passed on: who takes it to whom, how much slower, and what their word is worth.</summary>
        public static string Route(Notable n, Notable who, LetterChannel c)
        {
            if (n == null || who == null || n == who) return null;
            bool same = n.town == who.town;
            string word = LetterAddressMath.WordKey(n.role, NotableBook.Opinion(n)).Translate(n.Name.Named("NAME"), NotableBook.TitleOf(n).Named("TITLE")).Resolve();
            return LetterAddressMath.RouteKey(same, c == LetterChannel.Console).Translate(n.Name.Named("NAME"), who.Name.Named("HEADNAME"),
                NotableBook.TitleOf(who).Named("HEADTITLE"), (n.townName ?? "?").Named("TOWN"), (who.townName ?? "?").Named("HEADTOWN")).Resolve() + " " + word;
        }

        /// <summary>Who speaks for a town, for its card and the people's page: "Golakath, reeve, speaks for the town." or that nobody known does.</summary>
        public static string SpeaksFor(Settlement s)
        {
            if (s == null) return null;
            var head = HeadOf(s.ID);
            if (head == null) return "CC_Route_Speaks_None".Translate(s.Name.Named("TOWN")).Resolve();
            bool leader = LeaderOf(s.Faction) == head;
            return (leader ? "CC_Route_Speaks_Leader" : "CC_Route_Speaks").Translate(head.Name.Named("NAME"), NotableBook.TitleOf(head).Named("TITLE"), s.Name.Named("TOWN"),
                (s.Faction?.Name ?? "?").Named("PEOPLE")).Resolve();
        }
    }
}
