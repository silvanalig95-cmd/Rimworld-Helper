using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What an option of a letter is on Rimfolk's event card.</summary>
    public enum CardOptionRole
    {
        /// <summary>An answer: a card of its own.</summary>
        Answer,
        /// <summary>The letter's own close, postpone or reject: a button at the card's foot.</summary>
        Leave,
        /// <summary>The game's "Jump to location": the header's button.</summary>
        Jump,
        /// <summary>The game's "View related quest": a button at the foot.</summary>
        Quest,
        /// <summary>A link to an info card (the letter's hyperlinks): a chip under "See also".</summary>
        Link,
        /// <summary>Something the letter lets you do without answering it (look at someone): a quiet button at the foot.</summary>
        Act
    }

    /// <summary>
    /// The rules of Rimfolk's event cards (docs/design/rimfolk-look.md, stage 3, point 1), free of game types so Tools/Tests
    /// try them: what kind each of Rimfolk's letters is, what each of a letter's options becomes on the card, when the card
    /// may be closed without an answer, what the council leans to by its votes, how the card lays out as it grows, and the
    /// plain letters of Rimfolk's kept by their ID.
    /// </summary>
    public static class EventCardMath
    {
        // ---------------- The kinds ----------------

        /// <summary>
        /// The kind of each of Rimfolk's letters by its def, and of the game's own kinds Rimfolk sends its plain letters
        /// with; its word is "CC_EventCard_Kind_" and the kind. A def not here is a question if it has answers, else news.
        /// </summary>
        public static readonly Dictionary<string, string> Kinds = new Dictionary<string, string>
        {
            // The council's.
            ["Council_DilemmaLetter"] = "Question",
            ["Council_MeetingLetter"] = "Meeting",
            ["Council_CalendarLetter"] = "CalendarDay",
            ["Rimfolk_ElectionLetter"] = "Election",
            // Rimfolk's events and the world's.
            ["Council_EventLetter"] = "Event",
            ["Council_EventLetterThreat"] = "EventThreat",
            ["Council_WorldNews"] = "WorldNews",
            ["Council_WorldNewsBad"] = "WorldNewsBad",
            ["Council_WorldChoice"] = "World",
            ["Council_WorldChoiceThreat"] = "WorldThreat",
            // Camps on the colony's land.
            ["Council_CampLetter"] = "Camp",
            ["Rimfolk_CampLetterDanger"] = "CampDanger",
            // Other peoples.
            ["Rimfolk_TruceOffer"] = "Peoples",
            ["Rimfolk_PactOffer"] = "Peoples",
            ["Rimfolk_CallToArms"] = "Peoples",
            ["Rimfolk_FeastSuggest"] = "Peoples",
            ["Rimfolk_FealtyOffer"] = "Peoples",
            ["Rimfolk_VassalAsk"] = "Peoples",
            ["Rimfolk_FavourAsk"] = "Peoples",
            ["Rimfolk_MatchOffer"] = "Peoples",
            ["Rimfolk_WardOffer"] = "Peoples",
            ["Rimfolk_HostageForfeit"] = "Peoples",
            ["Rimfolk_RansomOffer"] = "Peoples",
            ["Rimfolk_RansomByHand"] = "Peoples",
            ["Rimfolk_EnvoyOffer"] = "Peoples",
            // The region and the colony's own.
            ["Rimfolk_HamletAsk"] = "Region",
            ["Rimfolk_RaidersCampTerms"] = "Region",
            ["Rimfolk_JourneyLetter"] = "Journey",
            ["Rimfolk_FoundingLetter"] = "Colonies",
            ["Rimfolk_TownLifeLetter"] = "Colonies",
            ["Council_NewsLetter"] = "News",
            ["Council_WelcomeLetter"] = "Rimfolk",
            ["Council_UpdateLetter"] = "Rimfolk",
            // A god's card (Gods/Presence/GodCard.cs): it opens its own dark card, a letter that tells.
            ["Rimfolk_GodCard"] = "Told",
            // The game's own kinds, as Rimfolk's plain letters use them.
            ["ThreatBig"] = "ThreatBig",
            ["ThreatSmall"] = "Threat",
            ["NegativeEvent"] = "Bad",
            ["PositiveEvent"] = "Good",
            ["NeutralEvent"] = "Neutral",
            ["Death"] = "Death"
        };

        /// <summary>Every kind's name, in the order its words are listed.</summary>
        public static readonly string[] KindNames =
        {
            "Question", "Meeting", "CalendarDay", "Election", "Event", "EventThreat", "WorldNews", "WorldNewsBad", "World", "WorldThreat", "Camp", "CampDanger",
            "Peoples", "Region", "Journey", "Colonies", "News", "Rimfolk", "ThreatBig", "Threat", "Bad", "Good", "Neutral", "Death", "Answer", "Told"
        };

        /// <summary>A letter's kind by its def's name: the table's, else a question (answers to choose) or a letter that tells.</summary>
        public static string KindOf(string defName, bool hasAnswers)
        {
            if (defName != null && Kinds.TryGetValue(defName, out var k)) return k;
            return hasAnswers ? "Answer" : "Told";
        }

        // ---------------- The options ----------------

        /// <summary>
        /// What an option becomes on the card, by what the game knows of it: a link to an info card; the letter's own close,
        /// postpone or reject (its words: <paramref name="leaveWords"/>), its jump to the place, its quest's page (the words
        /// it starts with); else an answer when it closes the letter or leads on (<paramref name="resolves"/>,
        /// <paramref name="leads"/>), and something to do beside answering when it does neither (a look at someone).
        /// </summary>
        public static CardOptionRole RoleOf(string text, bool resolves, bool leads, bool hyperlink, IList<string> leaveWords, string jumpWord, string questWord)
        {
            if (hyperlink) return CardOptionRole.Link;
            string t = text ?? "";
            if (leaveWords != null)
                foreach (var w in leaveWords)
                    if (!string.IsNullOrEmpty(w) && t == w)
                        return CardOptionRole.Leave;
            if (!string.IsNullOrEmpty(jumpWord) && t == jumpWord) return CardOptionRole.Jump;
            if (!string.IsNullOrEmpty(questWord) && t.StartsWith(questWord, StringComparison.Ordinal)) return CardOptionRole.Quest;
            return resolves || leads ? CardOptionRole.Answer : CardOptionRole.Act;
        }

        /// <summary>
        /// Whether the card may be closed without an answer (its close button and Escape), leaving the letter on the stack as
        /// the game's "Postpone" does: always, but on a letter's last tick before it runs out, when the game itself opens it
        /// and wants an answer (its postpone is greyed then).
        /// </summary>
        public static bool Closable(bool timeoutActive, int disappearAtTick, int now) => !(timeoutActive && disappearAtTick <= now + 1);

        // ---------------- What the council leans to ----------------

        /// <summary>
        /// The answer the council leans to by its votes (<paramref name="votes"/>: one per answer, how many are for it): the
        /// one most are for, if more are for it than for any other; -1 when nobody voted or the room is split.
        /// </summary>
        public static int Lean(IList<int> votes)
        {
            if (votes == null) return -1;
            int best = -1, most = 0;
            bool tied = false;
            for (int i = 0; i < votes.Count; i++)
            {
                if (votes[i] > most)
                {
                    most = votes[i];
                    best = i;
                    tied = false;
                }
                else if (votes[i] == most && most > 0) tied = true;
            }
            return tied ? -1 : best;
        }

        /// <summary>The votes for each answer from the voters' choices (each an answer's index; out of range ignored).</summary>
        public static int[] Votes(IList<int> choices, int answers)
        {
            var v = new int[Math.Max(0, answers)];
            if (choices != null)
                foreach (int c in choices)
                    if (c >= 0 && c < v.Length)
                        v[c]++;
            return v;
        }

        /// <summary>A meeting's vote to go ahead or not (a hand-over's): 0 for going ahead, 1 against, -1 when it is even or not held.</summary>
        public static int LeanForAgainst(bool voted, int forVotes, int against) => !voted || forVotes == against ? -1 : forVotes > against ? 0 : 1;

        // ---------------- The layout ----------------

        /// <summary>The card's least size: its header, a few lines of text and an answer, the foot's buttons; the rest scrolls.</summary>
        public const float LeastW = 520f, LeastH = 440f;

        /// <summary>The width it first opens at, and its height at most (more where the letter has much to say).</summary>
        public const float OpenW = 640f, OpenHMax = 780f;

        /// <summary>From this width of the card's body, who is involved and what the council leans to stand in a column beside the text.</summary>
        public const float SideFrom = 760f;

        /// <summary>The side column's width in a body this wide: none below <see cref="SideFrom"/>, then 30% of it, 240 to 340.</summary>
        public static float SideWidth(float bodyW) => bodyW < SideFrom ? 0f : Math.Max(240f, Math.Min(340f, (float)Math.Round(bodyW * 0.3f)));

        /// <summary>The gap between the main column and the side column.</summary>
        public const float SideGap = 16f;

        /// <summary>The main column's width beside the side column (the whole body without it).</summary>
        public static float MainWidth(float bodyW)
        {
            float side = SideWidth(bodyW);
            return side > 0f ? bodyW - side - SideGap : bodyW;
        }

        /// <summary>The answers' columns in a main column this wide: one, two from 620, three from 1080; never more than there are answers.</summary>
        public static int AnswerColumns(float mainW, int answers)
        {
            int cols = mainW >= 1080f ? 3 : mainW >= 620f ? 2 : 1;
            return Math.Max(1, Math.Min(cols, answers));
        }

        /// <summary>The gap between answer cards.</summary>
        public const float AnswerGap = 10f;

        /// <summary>An answer card's width in its column.</summary>
        public static float AnswerWidth(float mainW, int cols) => cols <= 1 ? mainW : (mainW - (cols - 1) * AnswerGap) / cols;

        /// <summary>The header's picture: one portrait or a thing this large; a few people overlap, each next one this much along.</summary>
        public const float PictureSize = 88f, PictureStep = 0.62f;

        /// <summary>The picture on a narrow card (below <see cref="PictureFullFrom"/> of body), so the title keeps its room.</summary>
        public const float PictureSmall = 64f, PictureFullFrom = 560f;

        /// <summary>The picture's size on a card whose body is this wide.</summary>
        public static float PictureSizeFor(float bodyW) => bodyW < PictureFullFrom ? PictureSmall : PictureSize;

        /// <summary>The header's jump button: its word's width and room around it, between these.</summary>
        public const float JumpLeast = 90f, JumpMost = 160f;

        /// <summary>The header's room around its picture and words, and the gap between the picture and the title.</summary>
        public const float HeadPad = 10f, HeadGap = 14f;

        /// <summary>The narrowest the title may be beside the picture and the jump button.</summary>
        public const float TitleLeast = 120f;

        /// <summary>The most people the header's picture shows (the rest are under "Who is involved").</summary>
        public const int PictureMost = 3;

        /// <summary>The header picture's width for this many people (0: a thing, an emblem or the kind's mark, one tile).</summary>
        public static float PictureWidth(int people, float size = PictureSize)
        {
            int n = Math.Max(1, Math.Min(PictureMost, people));
            return (float)Math.Round(size + (n - 1) * size * PictureStep);
        }

        /// <summary>The most people listed under "Who is involved"; more are named in its tooltip.</summary>
        public const int InvolvedMost = 8;

        /// <summary>A row of "Who is involved": a portrait, the name, the mood's bar and the heart's band.</summary>
        public const float PersonRowH = 54f, PersonPortrait = 42f;

        /// <summary>The most lines of what an answer costs or risks shown on its card; all of it on hover.</summary>
        public const int CostLinesMost = 3, RiskLinesMost = 2;

        /// <summary>The foot's height (its buttons) and the room above it.</summary>
        public const float FootH = 32f, FootGap = 8f;

        // ---------------- Rimfolk's plain letters ----------------

        /// <summary>The most plain letters of Rimfolk's kept by their ID (the game's History tab keeps a few hundred letters).</summary>
        public const int MaxPlain = 400;

        /// <summary>
        /// The plain letters kept: the given ones still on the stack or in the History tab (<paramref name="present"/>; null
        /// keeps every one), each once, the newest <see cref="MaxPlain"/> (a letter's ID only grows).
        /// </summary>
        public static List<int> Keep(IEnumerable<int> ids, ICollection<int> present, int max = MaxPlain)
        {
            var kept = new List<int>();
            if (ids == null) return kept;
            var seen = new HashSet<int>();
            foreach (int id in ids)
                if ((present == null || present.Contains(id)) && seen.Add(id))
                    kept.Add(id);
            kept.Sort();
            if (kept.Count > max) kept.RemoveRange(0, kept.Count - max);
            return kept;
        }
    }
}
