using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: each of the gods among the people now, on the map being looked at ("Rimfolk", "Gods among: ..."): a vowed
    /// pilgrimage set going, a fish given back, a debt paid or let lapse, the keeper's walk and writing, a raider's cry and a
    /// prisoner's tale, the grave's words, a season named, its day asked, and midwinter kept.
    /// </summary>
    public static class GodsAmongDebug
    {
        private static void Say(string text) => Messages.Message("Gods among: " + text, MessageTypeDefOf.SilentInput, false);

        private static Map Map => Find.CurrentMap;

        private static Pawn Selected => Find.Selector?.SingleSelectedThing as Pawn ?? Map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.Awake());

        private static bool Ready()
        {
            if (Map != null && GodsAmongBook.Book != null) return true;
            Say("the gods are off, or no game.");
            return false;
        }

        [DebugAction("Rimfolk", "Gods among: a vowed pilgrimage to a holy place now (selected)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Pilgrimage()
        {
            if (!Ready() || Selected == null) return;
            Say(VowPilgrimage.DevSend(Selected));
        }

        [DebugAction("Rimfolk", "Gods among: a fisher gives the first fish back (selected)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Fisher()
        {
            if (!Ready() || Selected == null) return;
            Say(MurmurFishers.DevGiveBack(Selected));
        }

        [DebugAction("Rimfolk", "Gods among: a debt paid now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebtPaid()
        {
            if (Ready()) Say(DucatDebts.DevPaid(Map));
        }

        [DebugAction("Rimfolk", "Gods among: a debt let lapse now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebtLapsed()
        {
            if (Ready()) Say(DucatDebts.DevLapsed(Map));
        }

        [DebugAction("Rimfolk", "Gods among: the keeper walks out to look", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void KeeperLook()
        {
            if (Ready()) Say(KeeperAtWork.DevLook(Map));
        }

        [DebugAction("Rimfolk", "Gods among: the keeper writes up the book", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void KeeperWrite()
        {
            if (Ready()) Say(KeeperAtWork.DevWrite(Map));
        }

        [DebugAction("Rimfolk", "Gods among: a raider cries their god's name", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RaiderCry()
        {
            if (Ready()) Say(RaidersGods.DevCry(Map));
        }

        [DebugAction("Rimfolk", "Gods among: a prisoner tells the warden of their gods", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PrisonerTale()
        {
            if (Ready()) Say(RaidersGods.DevPrisoner(Map));
        }

        [DebugAction("Rimfolk", "Gods among: the dead given to their god", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Grave()
        {
            if (Ready()) Say(GraveGods.DevGiven(Map));
        }

        [DebugAction("Rimfolk", "Gods among: a season named (the long cold)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SeasonCold()
        {
            if (Ready()) Say(SeasonGods.DevNamed(Map, "cold"));
        }

        [DebugAction("Rimfolk", "Gods among: a season named (the wet)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SeasonWet()
        {
            if (Ready()) Say(SeasonGods.DevNamed(Map, "wet"));
        }

        [DebugAction("Rimfolk", "Gods among: a season named (the hot summer)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SeasonHeat()
        {
            if (Ready()) Say(SeasonGods.DevNamed(Map, "heat"));
        }

        [DebugAction("Rimfolk", "Gods among: a named season comes round, the council asked", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SeasonAsk()
        {
            if (Ready()) Say(SeasonGods.DevAsk(Map));
        }

        [DebugAction("Rimfolk", "Gods among: midwinter kept today", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MidwinterNow()
        {
            if (Ready()) Say(Midwinter.DevKeep(Map));
        }

        [DebugAction("Rimfolk", "Gods among: what is under way (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Log()
        {
            var b = GodsAmongBook.Book;
            if (b == null)
            {
                Say("the gods are off.");
                return;
            }
            Verse.Log.Message("[Rimfolk] Gods among the people: tales owed " + b.tales.Count + "; the dead's gods kept " + b.deadHeld.Count + "; coin for "
                              + (b.coinFor?.LabelShort ?? "nobody") + "; trader to stay longer " + (b.longerUntil >= 0) + "; hard bargain waiting " + (b.hardUntil >= 0)
                              + ", by " + (b.hardTrader?.LabelShort ?? "nobody") + "; keeper looking " + (b.looker?.LabelShort ?? "no") + ", writing " + (b.writer?.LabelShort ?? "no")
                              + ", last wrote at " + b.keeperWrote + "; named seasons " + string.Join(", ", b.seasons.Select(s => s.text + " (" + s.year + ", " + Gods.Name(Gods.ById(s.god)) + (s.asked ? ", asked" : "") + ")"))
                              + "; midwinter " + (b.mwDay >= 0 ? "day " + b.mwDay + ", done " + b.mwDone : "none") + ".");
            Say("logged.");
        }
    }
}
