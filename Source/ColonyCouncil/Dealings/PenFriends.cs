using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Pen friends (docs/design/post-and-letters.md, step 2, point 12): a steady exchange of letters with one of their
    /// notables becomes something. Each letter of the colony's answered warmly or courteously is one of the exchange
    /// (NeighbourLetters.Read), and so is an answer to a letter they wrote first; three within 60 days with their regard at
    /// 30 or more make them a pen friend (DealingMath.Becomes), which holds while a letter still passes within 90 days and
    /// their regard stays at 15. A pen friend: warns the colony of a raid by a people whose towns lie near theirs (their
    /// messenger walks in with the word some hours before it comes), gives the colony a little better terms with their
    /// people's traders, introduces its letters to peoples beyond its range (the chain of neighbours, Post/PostReach.cs),
    /// and after a season offers a favor (a debt of honor of theirs, once a year at most).
    /// </summary>
    public static class PenFriends
    {
        public static bool On => (ColonyCouncilMod.Settings?.penFriends ?? true) && NeighbourLetters.On;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static DealingBook Book => DealingBook.Instance;

        private static string W(string key, params NamedArgument[] args) => Dealings.W(key, args);

        public static bool Is(Notable n) => n != null && On && (Book?.FriendOf(n.id, false)?.Friend ?? false);

        /// <summary>The colony has a pen friend among this people.</summary>
        public static bool AmongThem(Faction f)
        {
            var book = Book;
            if (f == null || book == null || !On) return false;
            foreach (var x in book.friends)
                if (x.Friend && NotableBook.Instance?.Get(x.notable) is Notable n && n.Alive && n.faction == f)
                    return true;
            return false;
        }

        public static int Count => On ? Book?.friends.Count(x => x.Friend) ?? 0 : 0;

        public static List<Notable> Friends()
        {
            var list = new List<Notable>();
            var book = Book;
            var nb = NotableBook.Instance;
            if (book == null || nb == null || !On) return list;
            foreach (var x in book.friends)
                if (x.Friend && nb.Get(x.notable) is Notable n && n.Alive)
                    list.Add(n);
            return list;
        }

        /// <summary>One of the exchange: a letter of the colony's answered warmly or courteously (or an answer to theirs). A friendship begins when the exchange is steady.</summary>
        public static void Exchanged(Notable n, AnswerTone tone)
        {
            var book = Book;
            if (n == null || book == null || !On || tone > AnswerTone.Cordial || NotableBook.Hostile(n)) return;
            try
            {
                int now = Now;
                var x = book.FriendOf(n.id, true);
                x.letters.Add(now);
                while (x.letters.Count > DealingMath.MaxExchanges) x.letters.RemoveAt(0);
                if (x.Friend || !DealingMath.Becomes(DealingMath.Recent(x.letters, now, DealingMath.ExchangeDays), NotableBook.Opinion(n))) return;
                x.since = now;
                // Too many: the coolest lapses.
                var all = book.friends.Where(o => o.Friend).ToList();
                if (all.Count > DealingMath.MaxFriends)
                {
                    var coolest = all.Where(o => o != x).OrderBy(o => NotableBook.Opinion(NotableBook.Instance?.Get(o.notable))).FirstOrDefault();
                    if (coolest != null) coolest.since = -1;
                }
                var home = HappeningTracker.HomeMap() ?? Homes.Seat;
                string text = W("CC_Pen_Text", NotableBook.Called(n).Named("CALLED"), n.Name.Named("NAME"), (n.townName ?? n.faction?.Name ?? "?").Named("TOWN"),
                    (n.faction?.Name ?? "?").Named("PEOPLE"), DealingMath.PenFriendOffset.ToStringPercent().Named("TERMS"));
                // Written out in full: their own word as a friend, and what it brings under it (Letters/LetterForm.cs).
                text = LetterForm.Friend(n, "friendmade", new List<string> { text })?.full ?? text;
                Tell.Letter(NewsKind.World, Importance.Notable, home, W("CC_Pen_Label", n.Name.Named("NAME")), text, LetterDefOf.PositiveEvent, LookTargets.Invalid, n.faction);
                Dealings.Changed();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pen friends: a letter exchanged", e);
            }
        }

        /// <summary>The day: a friendship lapses when the letters stop or their regard falls; a favor offered after a season; the gone let go.</summary>
        public static void Daily(int now)
        {
            var book = Book;
            var nb = NotableBook.Instance;
            if (book == null || nb == null) return;
            book.friends.RemoveAll(x => x == null || nb.Get(x.notable) == null || !nb.Get(x.notable).Alive);
            if (!On) return;
            foreach (var x in book.friends)
            {
                var n = nb.Get(x.notable);
                if (n == null) continue;
                // An exchange that never became one is let go after a long silence.
                if (!x.Friend)
                {
                    x.letters.RemoveAll(t => now - t > DealingMath.KeepDays * DealingMath.DayTicks);
                    continue;
                }
                int regard = NotableBook.Opinion(n);
                if (NotableBook.Hostile(n) || !DealingMath.Holds(DealingMath.Recent(x.letters, now, DealingMath.KeepDays), regard))
                {
                    x.since = -1;
                    Tell.Message(NewsKind.World, Importance.Notable, HappeningTracker.HomeMap(), W("CC_Pen_Lapsed", n.Name.Named("NAME")), LookTargets.Invalid,
                        MessageTypeDefOf.NeutralEvent);
                    Dealings.Changed();
                    continue;
                }
                float days = (now - x.since) / (float)DealingMath.DayTicks;
                float sinceFavour = x.favourAt < 0 ? -1f : (now - x.favourAt) / (float)DealingMath.DayTicks;
                if (DealingMath.FavourDue(days, sinceFavour) && FavoursOwed.On && Diplomacy.Deals(n.faction))
                {
                    var home = HappeningTracker.HomeMap() ?? Homes.Seat;
                    x.favourAt = now;
                    // A favour in person from the friend to the one who writes to them (the player, 10 November 2026: a
                    // friendship's, not their whole people's; half what their people's would bring: FavourMath.Effective).
                    var writer = Writer(n);
                    if (writer != null && FavoursOwed.MakePersonal(n, writer, "penFriend", DealingMath.FavourSize) != null)
                    {
                        string text = W("CC_Pen_Favour", n.Name.Named("NAME"), (n.faction?.Name ?? "?").Named("PEOPLE"), writer.Named("PAWN"));
                        text = LetterForm.Friend(n, "friendfavour", new List<string> { text })?.full ?? text;
                        Tell.Letter(NewsKind.World, Importance.Notable, home, W("CC_Pen_FavourLabel", n.Name.Named("NAME")), text, LetterDefOf.PositiveEvent, LookTargets.Invalid,
                            n.faction);
                    }
                }
            }
        }

        /// <summary>
        /// The colonist a pen friend owes their favour to: who wrote the last letter to them that is still kept, else the one
        /// of the colony they think best of; null when nobody of the colony's is left.
        /// </summary>
        private static Pawn Writer(Notable n)
        {
            var book = NotableBook.Instance;
            var last = book?.letters.Where(l => l != null && l.notable == n.id && KinMemory.Ours(l.writer)).OrderByDescending(l => l.created).FirstOrDefault();
            if (last != null) return last.writer;
            return n.regards.Where(r => r != null && KinMemory.Ours(r.pawn)).OrderByDescending(r => r.personal).FirstOrDefault()?.pawn;
        }

        /// <summary>A pen friend among a people introduces the colony's letters to the peoples they know (the chain of neighbours).</summary>
        public static bool Introduces(Faction f) => AmongThem(f);
    }

    /// <summary>
    /// Word of a raid before it comes (a watchtower built together toward one of their towns, a pen friend's warning): a
    /// raid the storyteller sends to a home, of a people whose nearest town lies that way or near the friend's town, goes
    /// into the game's own incident queue a few hours on (DealingMath.WarnHours), and the word comes now: a messenger of
    /// the tower's or the friend's people walks in from the edge with it (the letter opens when it is handed over at the
    /// gate), or over the radio when both have it. The raid's people is the one the game would have chosen (its own
    /// choice, made now instead of at the raid). Not while the colony's relay mast sings: the mast's own warning has it.
    /// </summary>
    public static class DealingWarnings
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static System.Reflection.MethodInfo resolve;
        private static bool looked;

        /// <summary>The game's own choice of a raid's people (IncidentWorker_Raid.TryResolveRaidFaction), made now.</summary>
        private static bool Resolve(IncidentWorker w, IncidentParms parms)
        {
            if (parms.faction != null) return true;
            if (!looked)
            {
                looked = true;
                resolve = AccessTools.Method(typeof(IncidentWorker_Raid), "TryResolveRaidFaction", new[] { typeof(IncidentParms) });
            }
            if (resolve == null) return false;
            try
            {
                return resolve.Invoke(w, new object[] { parms }) is bool ok && ok && parms.faction != null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: a raid's people chosen early", e);
                return false;
            }
        }

        /// <summary>True: the raid is held for the word, and the word sent; the storyteller doesn't fire it now.</summary>
        public static bool Warn(FiringIncident fi)
        {
            var map = fi?.parms?.target as Map;
            if (fi?.def?.Worker == null || map == null || !Homes.IsHome(map) || !Dealings.On) return false;
            if (!(fi.def.Worker is IncidentWorker_RaidEnemy) || fi.sourceQuestPart != null || fi.parms.quest != null) return false;
            var towers = Dealings.Towers();
            var friends = PenFriends.Friends().Where(n => !NotableBook.Hostile(n) && NotableBook.TownOf(n.town) != null).ToList();
            if (towers.Count == 0 && friends.Count == 0) return false;
            if (RelayMast.Working(map)) return false;
            if (!fi.def.Worker.CanFireNow(fi.parms) || !Resolve(fi.def.Worker, fi.parms)) return false;
            var raiders = fi.parms.faction;
            if (raiders == null || !raiders.HostileTo(Faction.OfPlayer)) return false;
            var near = WorldSituationWorker.NearestTown(raiders, map);
            if (near == null) return false;
            int now = Now;
            // A watchtower toward one of their towns, that way.
            Settlement tower = null;
            float raidersHeading = Outbox.Heading(map.Tile, near.Tile);
            foreach (var t in towers)
                if (DealingMath.ThatSide(Outbox.Heading(map.Tile, t.Tile), raidersHeading))
                {
                    tower = t;
                    break;
                }
            Notable friend = null;
            if (tower == null)
                foreach (var n in friends)
                {
                    var x = DealingBook.Instance?.FriendOf(n.id, false);
                    var town = NotableBook.TownOf(n.town);
                    if (x == null || town == null || n.faction == raiders || x.warnedAt >= 0 && now - x.warnedAt < DealingMath.WarnGapDays * DealingMath.DayTicks) continue;
                    if (LivingEvents.Distance(town.Tile, near.Tile) > DealingMath.WarnTiles) continue;
                    friend = n;
                    x.warnedAt = now;
                    break;
                }
            if (tower == null && friend == null) return false;
            int at = now + DealingMath.WarnHours(Rand.Value) * GenDate.TicksPerHour;
            if (!Find.Storyteller.incidentQueue.Add(new QueuedIncident(fi, at, 6 * GenDate.TicksPerHour))) return false;
            var from = tower?.Faction ?? friend.faction;
            string label = Dealings.W("CC_Warn_Label");
            string text = tower != null
                ? Dealings.W("CC_Warn_Tower", raiders.Name.Named("RAIDERS"), tower.Name.Named("TOWN"), (from?.Name ?? "?").Named("PEOPLE"))
                : Dealings.W("CC_Warn_Friend", raiders.Name.Named("RAIDERS"), friend.Name.Named("NAME"), (friend.townName ?? "?").Named("TOWN"));
            // A friend's word in haste, written out in full, the warning's plain lines under it (Letters/LetterForm.cs).
            if (tower == null) text = LetterForm.Friend(friend, "raidword", new List<string> { text }, raiders.Name)?.full ?? text;
            var townOf = tower ?? NotableBook.TownOf(friend.town);
            bool radio = NeighbourLetters.Console(map) != null && PostReach.Radio(from, townOf);
            if (radio)
            {
                Tell.Letter(NewsKind.World, Importance.Important, map, label, text + "\n\n" + Dealings.W("CC_Warn_ByRadio"), LetterDefOf.ThreatSmall,
                    new LookTargets(NeighbourLetters.Console(map)), from);
                return true;
            }
            var trip = Couriers.MailAnswer(map, from, new List<Thing>(), MailIn.MailId, now, Dealings.W("CC_Warn_Bring"), label, text, LetterDefOf.ThreatSmall, null, null);
            if (trip == null) Tell.Letter(NewsKind.World, Importance.Important, map, label, text, LetterDefOf.ThreatSmall, LookTargets.Invalid, from);
            else Tell.Message(NewsKind.World, Importance.Important, map, Dealings.W("CC_Warn_Coming", (from?.Name ?? "?").Named("PEOPLE")), LookTargets.Invalid,
                MessageTypeDefOf.NeutralEvent);
            return true;
        }
    }
}
