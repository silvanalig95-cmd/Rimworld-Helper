namespace ColonyCouncil
{
    /// <summary>
    /// Whether a Hospitality guest can be recruited, and how (Integrations/GuestCheck.cs shows it on the guest tab), from
    /// Hospitality's rules and Rimfolk's: people who keep no goodwill with anyone never let theirs go; a death acidifier
    /// keeps a guest from joining of their own will unless they're related to someone of the colony; a titled guest
    /// can't be taken by force; and joining of their own will takes friends enough here (royal friends' ranks for the
    /// titled) and, with Rimfolk's "Guests who would stay", a reason to stay, room and what they want of a home.
    /// </summary>
    public static class GuestCheckMath
    {
        public enum Verdict
        {
            /// <summary>Would join now, of their own will.</summary>
            Ready,
            /// <summary>Not of their own will yet: only by force for now.</summary>
            ForceOnly,
            /// <summary>Titled, so not by force, and not of their own will yet: once the conditions are met.</summary>
            NotYet,
            /// <summary>Not right now: still settling in, or in a mental state.</summary>
            NotNow,
            /// <summary>Their people keep no goodwill with anyone: never.</summary>
            Never,
            /// <summary>Titled and carrying a death acidifier: never, unless they come to be related to someone of the colony.</summary>
            NeverUnlessRelated
        }

        public static Verdict Of(bool goodwill, bool acidifier, bool related, bool titled, bool settled, bool enoughFriends, bool willing)
        {
            if (!goodwill) return Verdict.Never;
            bool forceOnly = acidifier && !related;
            if (titled && forceOnly) return Verdict.NeverUnlessRelated;
            if (!settled) return Verdict.NotNow;
            if (forceOnly) return Verdict.ForceOnly;
            if (enoughFriends && willing) return Verdict.Ready;
            return titled ? Verdict.NotYet : Verdict.ForceOnly;
        }

        /// <summary>Whether recruiting by force is open to them (not titled, and their people deal in goodwill).</summary>
        public static bool ForceOpen(bool goodwill, bool titled) => goodwill && !titled;
    }
}
