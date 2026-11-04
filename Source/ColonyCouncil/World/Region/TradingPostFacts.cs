using Verse;

namespace ColonyCouncil
{
    // The region's provider (Text/Facts/FactsRegion.cs) asks here for the trading posts near the colony
    // (World/Region/TradingPost.cs), as their records stand.
    public static partial class Facts
    {
        /// <summary>
        /// Trading posts: town:post when their town is one ({TOWN} its name, {POSTKIND} its kind by their age, {KEEPER} its
        /// keeper while one is known; current in its first month), town:post:warned once its keeper has written that it
        /// will close unless things mend; their:post when their people keep a post near the colony and their town is
        /// another ({POST}, {POSTKIND}); else region:post for another people's post near the colony ({POST},
        /// {POSTPEOPLE}).
        /// </summary>
        private static void Posts(FactScene s, FactSet f)
        {
            if (RegionBook.Instance == null) return;
            if (s.place != null)
            {
                var own = TradingPosts.PostAt(s.place.ID, out _);
                if (own != null)
                {
                    f.Tag("town:post", Fresh(Days(own.opened), 30f, 1.5f));
                    f.Value("POSTKIND", TradingPostWords.KindWord(own.people));
                    var keeper = TradingPosts.KeeperOf(own);
                    if (keeper != null) f.Value("KEEPER", TradingPostWords.KeeperName(keeper));
                    if (own.warnedAt >= 0) f.Tag("town:post:warned", 2f);
                }
            }
            var home = s.colony ?? s.speaker?.MapHeld;
            if (home == null) return;
            TradingPost theirs = null, other = null;
            foreach (var p in TradingPosts.PostsOf(home))
            {
                if (s.place != null && p.town == s.place.ID || p.name.NullOrEmpty()) continue;
                if (s.people != null && p.people == s.people) theirs ??= p;
                else other ??= p;
            }
            var post = theirs ?? other;
            if (post == null) return;
            f.Tag(theirs != null ? "their:post" : "region:post", Fresh(Days(post.opened), 30f, 1.2f));
            f.Value("POST", post.name);
            // Their own post's kind is their people's; another people's post is named, not its kind ({POSTKIND} stays theirs).
            if (theirs != null && !f.values.ContainsKey("POSTKIND")) f.Value("POSTKIND", TradingPostWords.KindWord(post.people));
            if (theirs == null && post.people != null) f.Value("POSTPEOPLE", post.people.Name);
        }
    }
}
