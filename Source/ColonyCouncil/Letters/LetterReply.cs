using System;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Writing back to a letter (the player, 10 November 2026: "i should also be able start a reply right from this window"):
    /// the act "Write back" on a received letter's event card, in the reading window and on the Diplomacy screen's People,
    /// beside each notable's letters. It opens the letter's window to them for what fits what came in
    /// (LetterKindMath.ReplyOf): the answer their letter written first waits for (Dealings/DealingActs.Reply), else
    /// greetings, else whatever can be written to them now; greyed with why when nothing can be (nobody at home who can
    /// write, no way for it to go, a letter to them already under way, the reasons each purpose gives).
    /// </summary>
    public static class LetterReply
    {
        private static Map Home => HappeningTracker.HomeMap() ?? Homes.Seat;

        /// <summary>"Write back".</summary>
        public static string Label => "CC_LetterRead_WriteBack".Translate().Resolve();

        /// <summary>Why no letter back can be written to them now, or null.</summary>
        public static string WhyNot(Notable n)
        {
            try
            {
                if (n == null || !n.Alive) return "CC_Letter_Why_Gone".Translate().Resolve();
                if (!NeighbourLetters.On) return "CC_Letter_Why_Off".Translate().Resolve();
                var map = Home;
                if (NeighbourLetters.Writers(map).Count == 0) return "CC_LetterRead_Why_NoWriter".Translate().Resolve();
                // No way for it to go: no messenger, carrier or rider, and no call.
                string byHand = NeighbourLetters.ChannelWhyNot(n, LetterChannel.Courier, map);
                if (byHand != null && NeighbourLetters.ChannelWhyNot(n, LetterChannel.Console, map) != null) return byHand;
                if (ReplyOf(n) == ReplyAs.Answer) return null;
                return NeighboursPanel.WhyNoLetter(n);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("letters: writing back", e);
                return "CC_Letter_Why_Gone".Translate().Resolve();
            }
        }

        /// <summary>What a letter back to them would be.</summary>
        public static ReplyAs ReplyOf(Notable n) =>
            LetterKindMath.ReplyOf(WorldWrites.Waiting(n).Count > 0 && DealingActs.ReplyWhyNot(n) == null, LetterOccasions.WhyNot(n, LetterPurpose.Greetings) == null);

        /// <summary>Opens the letter's window to them, for what fits (nothing when no letter can be written).</summary>
        public static void Open(Notable n)
        {
            if (WhyNot(n) != null) return;
            switch (ReplyOf(n))
            {
                case ReplyAs.Answer:
                    DealingActs.Reply(n);
                    break;
                case ReplyAs.Greetings:
                    Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Greetings, null));
                    break;
                default:
                    Find.WindowStack.Add(new Dialog_NeighbourLetter(n));
                    break;
            }
        }

        /// <summary>The notable an exchange is with, while the colony knows them.</summary>
        public static Notable Of(LetterExchange x) => x == null ? null : NotableBook.Instance?.Get(x.notable);

        /// <summary>The act on a received letter's card: "Write back", greyed with why; null for a letter that isn't one of theirs.</summary>
        public static DiaOption Act(Letter letter, out string label, out string why)
        {
            label = why = null;
            var x = letter != null ? LetterBook.ByLetter(letter.ID) : null;
            var told = x?.pages.FirstOrDefault(p => p.shownAs == letter.ID);
            var n = told != null && !told.ours ? Of(x) : null;
            if (n == null) return null;
            label = Label;
            why = WhyNot(n);
            var o = new DiaOption(label) { action = () => Open(n), resolveTree = false };
            if (why != null) o.Disable(why);
            return o;
        }
    }
}
