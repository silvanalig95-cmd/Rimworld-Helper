using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Why a people's goodwill toward the colony changed, as Rimfolk tells the game (World/GoodwillWhy.cs): every change
    /// Rimfolk makes passes the game one of its own reasons (a HistoryEventDef in Defs/HistoryEventDefs/Rimfolk_GoodwillReasons.xml),
    /// so the game's message says why ("Relations with Stormriders have changed from 53 to 45 (a letter that slighted
    /// their god).") and the Factions tab lists it among that people's recent events. Here: the reasons' names, the rule
    /// that keeps a change short of war, a slight's way from read to landed, and the check that finds a goodwill change
    /// with a message and no reason in the source. Pure, so Tools/Tests can check it.
    /// </summary>
    public static class GoodwillReasonMath
    {
        /// <summary>Every def of Rimfolk's starts so: the faction memory's hooks leave Rimfolk's own reasons alone.</summary>
        public const string Prefix = "Rimfolk_";

        // ---------------- The reasons, by what moved the goodwill ----------------

        // Letters, word and the colony's name.
        public const string Slight = "Rimfolk_GW_Slight", Letter = "Rimfolk_GW_Letter", Word = "Rimfolk_GW_Word", Name = "Rimfolk_GW_Name";
        // The council's answers and decrees; meetings and welcomes at the colony.
        public const string Council = "Rimfolk_GW_Council", Decree = "Rimfolk_GW_Decree", Meeting = "Rimfolk_GW_Meeting", Welcome = "Rimfolk_GW_Welcome";
        // Tasks, events and world events.
        public const string TaskDone = "Rimfolk_GW_TaskDone", TaskFailed = "Rimfolk_GW_TaskFailed", Event = "Rimfolk_GW_Event", WorldEvent = "Rimfolk_GW_WorldEvent";
        // Camps near the colony.
        public const string CampWell = "Rimfolk_GW_CampWell", CampSour = "Rimfolk_GW_CampSour", CampSent = "Rimfolk_GW_CampSent",
            CampRefused = "Rimfolk_GW_CampRefused", CampDefended = "Rimfolk_GW_CampDefended";
        // Fights, bouts and outsiders.
        public const string Brawl = "Rimfolk_GW_Brawl", Bout = "Rimfolk_GW_Bout", Challenge = "Rimfolk_GW_Challenge", Joined = "Rimfolk_GW_Joined", Gift = "Rimfolk_GW_Gift";
        // Dealings, orders and work.
        public const string DealKept = "Rimfolk_GW_DealKept", DealBroken = "Rimfolk_GW_DealBroken", Unpaid = "Rimfolk_GW_Unpaid",
            OrderFilled = "Rimfolk_GW_OrderFilled", OrderMissed = "Rimfolk_GW_OrderMissed", HardTimes = "Rimfolk_GW_HardTimes",
            Friendship = "Rimfolk_GW_Friendship", Together = "Rimfolk_GW_Together", TheirWork = "Rimfolk_GW_TheirWork";
        // The peoples' own events.
        public const string Helped = "Rimfolk_GW_Helped", Paid = "Rimfolk_GW_Paid", TurnedAway = "Rimfolk_GW_TurnedAway", Amends = "Rimfolk_GW_Amends",
            Ledger = "Rimfolk_GW_Ledger", PromiseKept = "Rimfolk_GW_PromiseKept", PromiseBroken = "Rimfolk_GW_PromiseBroken",
            DutyDone = "Rimfolk_GW_DutyDone", DutyRefused = "Rimfolk_GW_DutyRefused", Returned = "Rimfolk_GW_Returned",
            KeptTheirs = "Rimfolk_GW_KeptTheirs", Deserter = "Rimfolk_GW_Deserter", TheirDead = "Rimfolk_GW_TheirDead",
            ShrineLost = "Rimfolk_GW_ShrineLost", Harvest = "Rimfolk_GW_Harvest";
        // Diplomacy: schemes, fealty, wards, debts, truces and peace.
        public const string Caught = "Rimfolk_GW_Caught", Smear = "Rimfolk_GW_Smear", Tribute = "Rimfolk_GW_Tribute", Judged = "Rimfolk_GW_Judged",
            OathBroken = "Rimfolk_GW_OathBroken", Levies = "Rimfolk_GW_Levies", WardHome = "Rimfolk_GW_WardHome", WardDied = "Rimfolk_GW_WardDied",
            DebtRefused = "Rimfolk_GW_DebtRefused", Truce = "Rimfolk_GW_Truce", Peace = "Rimfolk_GW_Peace", Beholden = "Rimfolk_GW_Beholden";
        // Faiths and gods, as the colony knows them (never a god's own doing: the gods are unseen).
        public const string SharedGod = "Rimfolk_GW_SharedGod", RivalGod = "Rimfolk_GW_RivalGod", HolyWar = "Rimfolk_GW_HolyWar",
            Relic = "Rimfolk_GW_Relic", Feast = "Rimfolk_GW_Feast", Faith = "Rimfolk_GW_Faith", Sacred = "Rimfolk_GW_Sacred";

        /// <summary>Every reason above, once: each is a def with a label in Defs/HistoryEventDefs/Rimfolk_GoodwillReasons.xml.</summary>
        public static readonly string[] All =
        {
            Slight, Letter, Word, Name,
            Council, Decree, Meeting, Welcome,
            TaskDone, TaskFailed, Event, WorldEvent,
            CampWell, CampSour, CampSent, CampRefused, CampDefended,
            Brawl, Bout, Challenge, Joined, Gift,
            DealKept, DealBroken, Unpaid, OrderFilled, OrderMissed, HardTimes, Friendship, Together, TheirWork,
            Helped, Paid, TurnedAway, Amends, Ledger, PromiseKept, PromiseBroken, DutyDone, DutyRefused, Returned, KeptTheirs, Deserter,
            TheirDead, ShrineLost, Harvest,
            Caught, Smear, Tribute, Judged, OathBroken, Levies, WardHome, WardDied, DebtRefused, Truce, Peace, Beholden,
            SharedGod, RivalGod, HolyWar, Relic, Feast, Faith, Sacred,
        };

        /// <summary>
        /// One of Rimfolk's own reasons (these and the two older ones, a people hearing of a god's touch and the great white
        /// beast harmed): the faction memory's hooks skip them, since the code that changed the goodwill notes its own deed.
        /// </summary>
        public static bool Own(string defName) => defName != null && defName.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>A task for a people (Missions/): done or partly done raises their goodwill, failed or let go lowers it.</summary>
        public static string Task(int amount) => amount >= 0 ? TaskDone : TaskFailed;

        // ---------------- Never into a war ----------------

        /// <summary>Where a loss stops: Rimfolk's own events never turn a people hostile by themselves (the game's line is -75).</summary>
        public const int Floor = -60;

        /// <summary>
        /// The change a people's goodwill takes: a gain as it is, a loss only down to <paramref name="floor"/> (none when
        /// already below it).
        /// </summary>
        public static int NeverIntoWar(int amount, int goodwill, int floor = Floor) => amount > 0 ? amount : Math.Min(0, Math.Max(amount, floor - goodwill));

        // ---------------- A slight in a letter (Gods/World/GodLetters.cs) ----------------

        /// <summary>Its stages: written into the colony's letter; read at their end, their answer saying so on its way back.</summary>
        public const int SlightWritten = 0, SlightRead = 1;

        /// <summary>Their offence lands (their goodwill, their memory of it) once the answer that says so is in at the colony, and only once.</summary>
        public static bool SlightLands(int stage, bool done) => !done && stage == SlightRead;

        // ---------------- The source check ----------------

        /// <summary>
        /// The calls to the game's TryAffectGoodwillWith in a source file that let the game send its message (the argument
        /// left out, true or a variable) and give it no reason: each as its line number and its text. Lines that are
        /// comments are skipped.
        /// </summary>
        public static List<(int line, string call)> Unreasoned(string source)
        {
            var found = new List<(int, string)>();
            if (string.IsNullOrEmpty(source)) return found;
            const string name = "TryAffectGoodwillWith(";
            int i = 0;
            while ((i = source.IndexOf(name, i, StringComparison.Ordinal)) >= 0)
            {
                int start = i;
                i += name.Length;
                int lineStart = source.LastIndexOf('\n', start) + 1;
                // A comment, or the name in a string (this file's own), is not a call.
                if (source.Substring(lineStart, start - lineStart).Contains("//") || start > 0 && source[start - 1] == '"') continue;
                var args = Arguments(source, i, out int end);
                if (args == null) continue;
                i = end;
                string message = null, reason = null;
                int position = 0;
                foreach (var raw in args)
                {
                    string a = raw.Trim();
                    int colon = NamedColon(a);
                    if (colon > 0)
                    {
                        string key = a.Substring(0, colon).Trim(), value = a.Substring(colon + 1).Trim();
                        if (key == "canSendMessage") message = value;
                        else if (key == "reason") reason = value;
                        continue;
                    }
                    if (position == 2) message = a;
                    else if (position == 4) reason = a;
                    position++;
                }
                bool sends = message == null || message != "false";
                bool reasoned = reason != null && reason != "null";
                if (sends && !reasoned) found.Add((LineOf(source, start), source.Substring(start, end - start).Replace('\n', ' ')));
            }
            return found;
        }

        /// <summary>The arguments of a call whose "(" is just before <paramref name="from"/>, split at its own commas; null when it never closes.</summary>
        private static List<string> Arguments(string s, int from, out int end)
        {
            var args = new List<string>();
            int depth = 0, last = from;
            bool inString = false, inChar = false;
            for (int i = from; i < s.Length; i++)
            {
                char c = s[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (inChar)
                {
                    if (c == '\\') i++;
                    else if (c == '\'') inChar = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inString = true; break;
                    case '\'': inChar = true; break;
                    case '(': case '[': case '{': depth++; break;
                    case ')': case ']': case '}':
                        if (depth == 0)
                        {
                            if (c != ')') break;
                            if (i > last || args.Count > 0) args.Add(s.Substring(last, i - last));
                            end = i + 1;
                            return args;
                        }
                        depth--;
                        break;
                    case ',':
                        if (depth == 0)
                        {
                            args.Add(s.Substring(last, i - last));
                            last = i + 1;
                        }
                        break;
                }
            }
            end = s.Length;
            return null;
        }

        /// <summary>Where a named argument's colon is ("reason: x"), or -1 (a conditional's "a ? b : c" is not one).</summary>
        private static int NamedColon(string a)
        {
            int k = 0;
            if (k >= a.Length || !(char.IsLetter(a[k]) || a[k] == '_')) return -1;
            while (k < a.Length && (char.IsLetterOrDigit(a[k]) || a[k] == '_')) k++;
            int j = k;
            while (j < a.Length && char.IsWhiteSpace(a[j])) j++;
            return j < a.Length && a[j] == ':' && (j + 1 >= a.Length || a[j + 1] != ':') ? j : -1;
        }

        private static int LineOf(string s, int index)
        {
            int n = 1;
            for (int i = 0; i < index && i < s.Length; i++)
                if (s[i] == '\n') n++;
            return n;
        }
    }
}
