using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The teller: every Rimfolk system tells the player through here, and the teller picks the channel by how much a
    /// happening matters (its importance and its kind) and by how busy the colony's day already is (TellMath.cs has the
    /// rules), so that as the mod grows the player isn't flooded.
    /// <list type="bullet">
    /// <item><see cref="Message"/>: something that happened, as a message now. The "How many messages" setting decides
    /// by its importance whether it's told at all; the same text (or key) within <see cref="TellMath.MergeHours"/> hours
    /// is told once, by whichever channel; something hard to miss is always shown; something worth knowing is shown
    /// until the colony's messages for the day are reached, and on a busy day (half of them shown, or a threat or
    /// something hard to miss in the last hours) one person's news, health and the land's wait for the evening's colony
    /// news while the colony's and the world's still come. Nothing held back is lost.</item>
    /// <item><see cref="Minor"/>: the smaller news (what <see cref="News.Post"/> carries), to the evening's colony news, a
    /// message or the History tab by the "Colony news" setting; as a message it is weighed as messages are. The evening's
    /// letter carries the lines that matter most (<see cref="TellMath.EveningLines"/>); the rest go to the History tab.</item>
    /// <item><see cref="Letter"/>: a letter that only tells. Past the colony's letters for the day, or a person's or the
    /// land's on a busy day, it becomes a line in the evening's news. Important letters are never held back.</item>
    /// <item><see cref="Chronicle"/>: a line for the colony's story, written into the one history; the chronicle shows
    /// what its view selects (Chronicle.cs).</item>
    /// <item><see cref="Ask"/> and <see cref="CanAsk"/>: Rimfolk's questions (choice letters), never capped, spaced as
    /// <see cref="LetterGap"/> spaces them.</item>
    /// </list>
    /// With more than one colony, what happened at one of them names its settlement (<see cref="Homes.Name"/>); with one,
    /// the text is as it always was. Answers to the player's own click (a rejection, a button's confirmation, a window's
    /// or designator's feedback) stay direct <c>Messages.Message</c> calls, for the game's own sounds and timing.
    /// </summary>
    public static class Tell
    {
        public static int MessagesADay => Math.Max(TellMath.MinMessagesADay, ColonyCouncilMod.Settings?.messagesADay ?? TellMath.DefaultMessagesADay);

        public static int LettersADay => Math.Max(TellMath.MinLettersADay, ColonyCouncilMod.Settings?.lettersADay ?? TellMath.DefaultLettersADay);

        private static readonly TellBook book = new TellBook();
        private static Game bookGame;

        /// <summary>What has been told lately, forgotten with a new game or a load.</summary>
        private static TellBook Book
        {
            get
            {
                if (Current.Game != bookGame)
                {
                    bookGame = Current.Game;
                    book.Clear();
                }
                return book;
            }
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- Messages ----------------

        /// <summary>
        /// Something that happened, told as a message now. <paramref name="map"/> is where it happened (null: the map of
        /// <paramref name="look"/>); <paramref name="key"/> makes two messages with different words the same news (null:
        /// the text does).
        /// </summary>
        public static void Message(NewsKind kind, Importance importance, Map map, string text, LookTargets look = null, MessageTypeDef type = null,
            string key = null, bool historical = false)
        {
            if (text.NullOrEmpty() || !Notify.Shows(importance)) return;
            // Links are for Rimfolk's own windows: a marker never reaches the game's (UI/Cards/LinkTextMath.cs).
            text = LinkTextMath.Plain(text);
            AtOnce(kind, importance, map, text, look, type, key, historical);
        }

        /// <summary>
        /// The smaller news: a line in the evening's colony news, a message as it happens, or only the History tab, by the
        /// "Colony news" setting (UI/News.cs). With an importance, "How many messages" first decides whether it's told at
        /// all; without one it always is.
        /// </summary>
        public static void Minor(NewsKind kind, Importance? importance, Map map, string text, LookTargets look, MessageTypeDef type = null)
        {
            if (text.NullOrEmpty()) return;
            if (importance.HasValue && !Notify.Shows(importance.Value)) return;
            text = LinkTextMath.Plain(text);
            try
            {
                switch (News.Mode)
                {
                    case NewsMode.Digest:
                        var waiting = NewsComponent.Instance;
                        if (waiting == null) break;
                        var home = HomeOf(map, look);
                        int colony = ColonyOf(home, look);
                        // Told once: the same words shown as a message a little while ago aren't the evening's news too.
                        if (Book.Told("M|" + colony + "|" + text, Now)) return;
                        var item = Item(kind, home, text, look, type);
                        item.weight = WeightOf(importance ?? Importance.Notable);
                        waiting.Add(item);
                        Book.Note(colony, "evening", Now);
                        return;
                    case NewsMode.Off:
                        if (Find.Archive == null) break;
                        News.Archive(Item(kind, HomeOf(map, look), text, look, type));
                        return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("tell: colony news", e);
            }
            AtOnce(kind, importance ?? Importance.Notable, map, text, look, type, null, false);
        }

        private static int WeightOf(Importance i) => i == Importance.Important ? TellWeight.Important : i == Importance.Notable ? TellWeight.Notable : TellWeight.Flavor;

        /// <summary>A threat on the colony's home (the storyteller's raid) in the last few hours makes its day busy, as something hard to miss told would.</summary>
        private static void NoteThreat(Map home, int colony)
        {
            int threat = home?.storyState?.LastThreatBigTick ?? -1;
            if (threat > 0) Book.Important(colony, threat, Now);
        }

        private static void AtOnce(NewsKind kind, Importance importance, Map map, string text, LookTargets look, MessageTypeDef type, string key, bool historical)
        {
            NewsItem item = null;
            var verdict = TellVerdict.Show;
            try
            {
                var home = HomeOf(map, look);
                int colony = ColonyOf(home, look);
                item = Item(kind, home, text, look, type);
                item.weight = WeightOf(importance);
                NoteThreat(home, colony);
                verdict = Book.Judge("M|" + colony + "|" + (key ?? text), colony, (int)kind, false, item.weight, MessagesADay, Now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("tell: a message", e);
            }
            item ??= new NewsItem { text = text, kind = kind, look = look, type = type ?? MessageTypeDefOf.NeutralEvent };
            if (verdict == TellVerdict.Merged) return;
            if (verdict == TellVerdict.Held && Hold(item)) return;
            News.Show(item, historical);
        }

        // ---------------- Letters ----------------

        /// <summary>
        /// A letter that only tells (no choice to make). Past the day's cap for its kind and colony it becomes a line in
        /// the evening's colony news instead; the same letter within a few hours comes once. <paramref name="map"/> is
        /// where it happened (null: the map of <paramref name="look"/>).
        /// </summary>
        public static void Letter(NewsKind kind, Importance importance, Map map, string label, string text, LetterDef def, LookTargets look = null,
            Faction faction = null, string key = null)
        {
            if (label.NullOrEmpty() && text.NullOrEmpty()) return;
            label = LinkTextMath.Plain(label);
            text = LinkTextMath.Plain(text);
            def ??= LetterDefOf.NeutralEvent;
            look ??= LookTargets.Invalid;
            // A threat or a death is never held back, whatever the sender said.
            if (def == LetterDefOf.ThreatSmall || def == LetterDefOf.ThreatBig || def == LetterDefOf.Death) importance = Importance.Important;
            string body = text ?? "";
            NewsItem line = null;
            var verdict = TellVerdict.Show;
            try
            {
                var home = HomeOf(map, look);
                int colony = ColonyOf(home, look);
                NoteThreat(home, colony);
                verdict = Book.Judge("L|" + colony + "|" + (key ?? label + "\n" + text), colony, (int)kind, true, WeightOf(importance), LettersADay, Now);
                if (verdict == TellVerdict.Held)
                {
                    line = Item(kind, home, TellMath.LetterLine(label, text), look, TypeOf(def));
                    line.weight = WeightOf(importance);
                }
                body = NamedLetter(body, home, label);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("tell: a letter", e);
                if (verdict == TellVerdict.Held && line == null) verdict = TellVerdict.Show;
            }
            if (verdict == TellVerdict.Merged) return;
            if (verdict == TellVerdict.Held && Hold(line)) return;
            Find.LetterStack.ReceiveLetter(label ?? "", body, def, look, faction);
        }

        // ---------------- The colony's story ----------------

        /// <summary>
        /// A line for the colony's story: the teller writes it into the one history (World/Ledger/History.cs), which keeps
        /// it by its own rules; the chronicle shown is the history's view of the colony and selects what it shows
        /// (<see cref="ColonyCouncil.Chronicle"/>). Every system's <c>AddChronicle</c> comes here.
        /// </summary>
        public static void Chronicle(ChronicleEntry entry)
        {
            if (entry == null || entry.text.NullOrEmpty()) return;
            History.FromChronicle(entry);
            try
            {
                var home = entry.about?.MapHeld;
                Book.Note(ColonyOf(home != null && Homes.IsHome(home) ? home : null, entry.about != null ? new LookTargets(entry.about) : null), "chronicle", Now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("tell: the chronicle's tally", e);
            }
        }

        // ---------------- Questions ----------------

        /// <summary>Whether one of Rimfolk's questions that can wait may come now (<see cref="LetterGap.Open"/>).</summary>
        public static bool CanAsk => LetterGap.Open;

        /// <summary>
        /// One of Rimfolk's questions (a choice letter): never capped or held back; questions that can wait check
        /// <see cref="CanAsk"/> first. With more than one colony its text names the settlement it's about.
        /// </summary>
        public static void Ask(Letter letter, Map map = null)
        {
            if (letter == null) return;
            try
            {
                if (letter is ChoiceLetter c)
                {
                    var home = HomeOf(map, c.lookTargets);
                    if (home != null && Homes.All.Count > 1) c.Text = NamedLetter(c.Text, home, c.Label);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("tell: a question", e);
            }
            // Never capped, but part of how busy the day was (the evening's room).
            Book.Asked(Now);
            Find.LetterStack.ReceiveLetter(letter);
        }

        // ---------------- The evening ----------------

        /// <summary>Kept for the evening's colony news, whatever the "Colony news" setting. False when there's nowhere to keep it.</summary>
        private static bool Hold(NewsItem item)
        {
            var waiting = NewsComponent.Instance;
            if (waiting == null || item == null) return false;
            item.held = true;
            waiting.Add(item);
            return true;
        }

        /// <summary>The evening's letter's room tonight: fewer lines the more letters and messages the day already showed (TellMath.EveningRoom).</summary>
        public static int EveningRoom => TellMath.EveningRoom(Book.ShownToday(Now));

        /// <summary>The evening's lines that didn't fit its letter, gone to the History tab: counted in the day's tally.</summary>
        internal static void NoteHistory(int n)
        {
            if (n <= 0) return;
            for (int i = 0; i < n; i++) Book.Note(TellMath.NoColony, "history", Now);
        }

        /// <summary>The day so far, colony by colony: what went by each channel (the dev log's measure of a day).</summary>
        public static string Today()
        {
            var sb = new System.Text.StringBuilder();
            int now = Now;
            foreach (int c in Book.ColoniesToday(now))
            {
                var map = c == TellMath.NoColony ? null : Homes.MapOf(c);
                string name = c == TellMath.NoColony ? "beyond the homes" : map != null ? Homes.Name(map) ?? map.ToString() : "colony " + c;
                sb.Append(name).Append(": ");
                sb.Append(string.Join(", ", new[] { "letter", "message", "evening", "history", "merged", "chronicle" }.Select(ch => Book.Tally(c, ch, now) + " " + ch)));
                sb.Append('\n');
            }
            return sb.Length == 0 ? "Nothing told today." : sb.ToString().TrimEnd();
        }

        [LudeonTK.DebugAction("Rimfolk", "Teller: what was told today", allowedGameStates = LudeonTK.AllowedGameStates.Playing)]
        private static void DebugToday() => Log.Message("[Rimfolk] The teller today (day " + (Now / Sched.DayTicks) + "):\n" + Today());

        private static NewsItem Item(NewsKind kind, Map home, string text, LookTargets look, MessageTypeDef type)
        {
            // A copy of the places, so what's kept for the evening isn't shared with (or changed under) whoever sent it.
            var places = look?.targets != null && look.IsValid ? new LookTargets(look.targets) : null;
            return new NewsItem { text = Named(text, home), kind = kind, look = places, type = type ?? MessageTypeDefOf.NeutralEvent };
        }

        /// <summary>How a letter would have come as a message: good news, bad news, or neither.</summary>
        private static MessageTypeDef TypeOf(LetterDef def)
        {
            if (def == LetterDefOf.PositiveEvent) return MessageTypeDefOf.PositiveEvent;
            if (def == LetterDefOf.NegativeEvent || def == LetterDefOf.ThreatSmall || def == LetterDefOf.ThreatBig || def == LetterDefOf.Death)
                return MessageTypeDefOf.NegativeEvent;
            return MessageTypeDefOf.NeutralEvent;
        }

        // ---------------- Which colony ----------------

        /// <summary>The home it happened at: the map given, else the map of its first place; null when that's no home.</summary>
        private static Map HomeOf(Map map, LookTargets look)
        {
            if (map == null && look != null && look.IsValid)
            {
                var t = look.TryGetPrimaryTarget();
                map = t.Map ?? (t.Thing as Pawn)?.MapHeld;
            }
            return map != null && Homes.IsHome(map) ? map : null;
        }

        /// <summary>The colony it's counted under: the home it happened at, else the home of the person it's about, else none.</summary>
        private static int ColonyOf(Map home, LookTargets look)
        {
            if (home != null) return Homes.KeyOf(home);
            if (look != null && look.IsValid && look.TryGetPrimaryTarget().Thing is Pawn p && p.IsColonist)
            {
                var of = Homes.Of(p);
                if (of != null) return Homes.KeyOf(of);
            }
            return TellMath.NoColony;
        }

        /// <summary>With more than one colony, the settlement's name before the text (unless the text names it already).</summary>
        private static string Named(string text, Map home)
        {
            string name = NameFor(home, text, null);
            return name == null ? text : "CC_Tell_At".Translate(name.Named("PLACE"), text.Named("TEXT")).Resolve();
        }

        private static string NamedLetter(string text, Map home, string label)
        {
            string name = NameFor(home, text, label);
            return name == null ? text : "CC_Tell_AtLetter".Translate(name.Named("PLACE"), text.Named("TEXT")).Resolve();
        }

        private static string NameFor(Map home, string text, string label)
        {
            if (home == null || Homes.All.Count <= 1) return null;
            string name = Homes.Name(home);
            if (name.NullOrEmpty()) return null;
            if ((text ?? "").IndexOf(name, StringComparison.Ordinal) >= 0 || (label ?? "").IndexOf(name, StringComparison.Ordinal) >= 0) return null;
            return name;
        }
    }
}
