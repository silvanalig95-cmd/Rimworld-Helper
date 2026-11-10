using System;

namespace ColonyCouncil
{
    /// <summary>
    /// Diplomacy's base (docs/design/stage2-diplomacy.md, "Shared rules"; release 2a.1), with no game types so Tools/Tests
    /// can check it: the history's family of its own for what isn't a tie (<c>dip:</c>), the deeds the peoples remember
    /// for it (the whole list, so later releases only use them), the region pages' places, the letter purposes and the
    /// right-click acts in the order the design gives them (each release switches on its own), and a people's stance in
    /// words. Favours owed and called in have their own rules (FavourMath.cs), the nature service its own (NatureMath.cs).
    /// </summary>
    public static class DiplomacyMath
    {
        // ---------------- The history ----------------

        /// <summary>What isn't a tie goes into the history's family "dip"; ties write "tie:KIND:..." already.</summary>
        public const string Family = "dip";

        /// <summary>A word put in for the colony with a third people (a debt of honour called in).</summary>
        public const string Word = "dip:word";
        /// <summary>A people's promise to speak for peace with a people at war with the colony (a debt called in).</summary>
        public const string Peace = "dip:peace";
        /// <summary>A colonist they held, walked to the colony's land (a debt called in).</summary>
        public const string Freed = "dip:freed";
        /// <summary>The colony's word given on a people's side in their dispute (their debt called in).</summary>
        public const string Side = "dip:side";

        /// <summary>
        /// Pacts and truces (release 2a.2): their offer of a truce or a pact, a truce sworn at the gate, their band walked in
        /// on a call, the colony's fighters at their town on theirs.
        /// </summary>
        public const string TruceOffer = "dip:truceOffer", PactOffer = "dip:pactOffer", Parley = "dip:parley", Band = "dip:band", Arms = "dip:arms";

        /// <summary>Kinds the later releases write (design, "The history and the memory"): named here so the family is whole.</summary>
        public const string Ransomed = "dip:ransomed", Exchanged = "dip:exchanged", Envoy = "dip:envoy", Feast = "dip:feast", Contest = "dip:contest",
            Wedding = "dip:wedding", Task = "dip:task", Tribute = "dip:tribute", Levy = "dip:levy", FoundOut = "dip:foundout", Renown = "dip:renown";

        /// <summary>Wards and hostages (release 2b.3): a ward come or gone home, the colony's youth set out or back; a hostage given, returned, forfeit or held.</summary>
        public const string Ward = "dip:ward", Hostage = "dip:hostage";

        /// <summary>A match the colony proposed, turned down by their people: none sought with them again for a season (Matches.SeekWhyNot).</summary>
        public const string MatchRefused = "dip:matchNo";

        public static readonly string[] Kinds =
        {
            Word, Peace, Freed, Side, TruceOffer, PactOffer, Parley, Band, Arms, Ransomed, Exchanged, Envoy, Feast, Contest, Wedding, Task, Tribute, Levy, FoundOut, Renown,
            Ward, Hostage, MatchRefused
        };

        // ---------------- The deeds ----------------

        /// <summary>
        /// The deeds stage 2 adds to what the peoples remember (FactionMemory.Kinds), with their sides and sizes as the
        /// design gives them (the side as DeedSide names it): of the colony, its wrongs, and theirs.
        /// </summary>
        public static readonly (string kind, string side, int size)[] Deeds =
        {
            ("swore", "Favor", 8), ("keptWord", "Favor", 10), ("ransomed", "Favor", 4), ("envoyHonored", "Favor", 8),
            ("fostered", "Favor", 12), ("married", "Favor", 15), ("hostageReturned", "Favor", 6), ("levies", "Favor", 8),
            ("tribute", "Favor", 1),
            ("brokeTruce", "Wrong", 25), ("brokePact", "Wrong", 20), ("wordBroken", "Wrong", 4), ("spouseWronged", "Wrong", 30),
            ("schemed", "Wrong", 15), ("renounced", "Wrong", 20), ("tributeShort", "Wrong", 6),
            ("brokeWord", "TheirWrong", 12), ("truceBroken", "TheirWrong", 20), ("schemedUs", "TheirWrong", 12),
            ("protected", "TheirFavor", 10)
        };

        // ---------------- Where the player acts ----------------

        /// <summary>The region window's pages: Diplomacy after Letters (30), Household right after it, before Journeys (40).</summary>
        public const float DiplomacyOrder = 35f, HouseholdOrder = 36f;

        /// <summary>The letter purposes stage 2 adds, in its order; the first three are written (Favour in 2a.1, Pact and Truce in 2a.2).</summary>
        public static readonly string[] Purposes = { "Favour", "Pact", "Truce", "Match", "Ward", "Ransom", "Envoy", "Fealty" };

        /// <summary>The right-click acts stage 2 adds after Invite, in its order; the first two are offered (AskFavour in 2a.1, SpeakPact in 2a.2).</summary>
        public static readonly string[] Acts = { "AskFavour", "SpeakPact", "SpeakMatch", "OfferExchange" };

        // ---------------- A people's stance ----------------

        /// <summary>A people's stance toward the colony, in a word's key: allied, friendly, at peace, cool, hostile.</summary>
        public static string StanceKey(int goodwill, bool hostile, bool ally)
        {
            if (hostile) return "Hostile";
            if (ally || goodwill >= 75) return "Allied";
            if (goodwill >= 25) return "Friendly";
            if (goodwill >= 0) return "Peace";
            return "Cool";
        }

        /// <summary>A people is shown on the Diplomacy page: a town of theirs within reach, or a tie with the colony.</summary>
        public static bool Shown(bool inReach, bool anyTie) => inReach || anyTie;

        // ---------------- The page's model ----------------

        /// <summary>The pages and the town panel build their model at most this often while open (seconds of real time).</summary>
        public const float RebuildSeconds = 2f;

        /// <summary>Built again: never built, the ledger, the memory or the notables moved since, or older than the wait.</summary>
        public static bool Stale(float builtAt, float now, int builtVersion, int version) =>
            builtAt < 0f || builtVersion != version || now - builtAt >= RebuildSeconds;

        /// <summary>One number for "something the page shows changed": the ledger's ties, the memory, the notables, the history.</summary>
        public static int Version(int ledger, int memory, int notables, int history) =>
            unchecked(((ledger * 397) ^ memory) * 397 ^ notables) * 397 ^ history;
    }
}
