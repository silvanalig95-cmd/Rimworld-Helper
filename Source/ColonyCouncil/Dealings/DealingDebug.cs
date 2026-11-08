using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode (Debug actions, "Rimfolk"): the dealings, pen friends and the world's letters in the log; every council
    /// agrees for testing; the next thing a dealing waits for now (a season's delivery, the fair's morning, the work done);
    /// a notable writes to the colony now; the colony's warmest notable a pen friend now.
    /// </summary>
    public static class DealingDebug
    {
        /// <summary>Dev mode: every council agrees to what is asked (testing); not saved.</summary>
        public static bool AllAgree;

        private static int Now => Find.TickManager.TicksGame;

        [DebugAction("Rimfolk", "Dealings: the dealings, pen friends and the world's letters (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLog()
        {
            var book = DealingBook.Instance;
            if (book == null)
            {
                Log.Message("[Rimfolk] Dealings: no book in this game.");
                return;
            }
            var lines = book.deals.Select(d => $"#{d.id} {d.kind}/{DealingMath.SubName(d.kind, d.sub)} {d.people?.Name} {d.townName} {d.stage} next={d.next} until={d.until} "
                                               + $"goods={d.goods}x{d.count} silver={d.silver} done={d.done} missed={d.missed} trip={d.tripId} camp={d.campId} yes={d.yes}").ToList();
            var nb = NotableBook.Instance;
            lines.AddRange(book.friends.Select(f => $"pen friend {nb?.Get(f.notable)?.Name ?? "?"}: {(f.Friend ? "friend since " + f.since : "exchange")}, letters {f.letters.Count}"));
            lines.AddRange(book.asks.Select(a => $"letter #{a.id} from {nb?.Get(a.notable)?.Name ?? "?"}: {a.kind} {a.goods}x{a.count} opened={a.opened} answered={a.answered}"));
            lines.Add("terms by people: " + string.Join(", ", Find.FactionManager.AllFactionsListForReading.Where(f => !f.IsPlayer && Dealings.Terms(f) > 0f)
                .Select(f => f.Name + " " + Dealings.Terms(f).ToStringPercent())));
            lines.Add("towers: " + string.Join(", ", Dealings.Towers().Select(t => t.Name)));
            Log.Message("[Rimfolk] Dealings:\n" + string.Join("\n", lines));
        }

        [DebugAction("Rimfolk", "Dealings: every council agrees (testing, toggle)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAllAgree()
        {
            AllAgree = !AllAgree;
            Log.Message("[Rimfolk] Dealings: every council agrees: " + AllAgree);
        }

        [DebugAction("Rimfolk", "Dealings: what each dealing waits for, now (testing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNext()
        {
            var book = DealingBook.Instance;
            if (book == null) return;
            int n = 0;
            foreach (var d in book.deals.Where(x => x.stage == DealStage.Running && x.next >= 0))
            {
                // The day's morning hours, so a delivery or the fair can happen when the next hour is looked at.
                d.next = Now - 1;
                n++;
            }
            Log.Message($"[Rimfolk] Dealings: {n} dealings will do what they wait for at the next hour that suits them (a delivery 8 to 15, the fair's morning 7 to 12).");
        }

        [DebugAction("Rimfolk", "Dealings: a notable writes to the colony now (testing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWrite()
        {
            var home = Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;
            var n = NotableBook.Instance?.notables.Where(x => x != null && x.Alive && !NotableBook.Hostile(x) && NotableBook.TownOf(x.town) != null && x.pawn != null && !x.pawn.Spawned)
                .OrderByDescending(NotableBook.Opinion).FirstOrDefault();
            var a = n != null ? WorldWrites.Write(n, home, Now) : null;
            Log.Message("[Rimfolk] Dealings: " + (a != null ? $"{n.Name} wrote ({a.kind}); it comes in the post." : "nobody could write (no notable known at peace)."));
        }

        [DebugAction("Rimfolk", "Dealings: the warmest notable a pen friend now (testing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFriend()
        {
            var book = DealingBook.Instance;
            var n = NotableBook.Instance?.notables.Where(x => x != null && x.Alive && !NotableBook.Hostile(x)).OrderByDescending(NotableBook.Opinion).FirstOrDefault();
            if (book == null || n == null) return;
            var f = book.FriendOf(n.id, true);
            if (n.personal < DealingMath.FriendRegard) n.personal = DealingMath.FriendRegard + 10f;
            for (int i = 0; i < DealingMath.FriendLetters; i++) PenFriends.Exchanged(n, AnswerTone.Warm);
            Log.Message("[Rimfolk] Dealings: " + n.Name + (f.Friend ? " is a pen friend." : " is not a pen friend yet (regard " + NotableBook.Opinion(n) + ")."));
        }
    }
}
