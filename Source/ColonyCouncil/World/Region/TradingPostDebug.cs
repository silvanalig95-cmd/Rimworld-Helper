using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force each part of a trading post at the home on screen (Debug actions, "Rimfolk"): open one now for a
    /// people of your choosing, bring its keeper's warning now, sack it now, or close it one of the ways. The rules of who
    /// and when (goodwill, trade, distance, the look's chance, the gaps) are skipped; where it stands and what follows
    /// play by them.
    /// </summary>
    public static partial class TradingPosts
    {
        private static Map DebugHome => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static void Say(string text) => Messages.Message("Trading post: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Trading post: open one now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOpen()
        {
            var home = DebugHome;
            var ph = Of(home);
            if (ph == null) return;
            var options = new List<FloatMenuOption>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(x => CanOpen(x) && !x.HostileTo(Faction.OfPlayer)))
            {
                var people = f;
                bool has = ph.posts.Any(p => p.people == people);
                options.Add(new FloatMenuOption(people.Name + " (" + TradingPostWords.KindWord(people) + ")" + (has ? ": has one here" : ""), has ? (System.Action)null : () =>
                {
                    if (Open(home, ph, people, Now) == null) Say("no tile 6 to 12 tiles off can hold it.");
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no people at peace keeps town traders)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>The home's posts to choose from, then <paramref name="act"/> on the one chosen.</summary>
        private static void DebugPick(System.Action<Map, TradingPostHome, TradingPost, Settlement> act)
        {
            var home = DebugHome;
            var ph = Of(home);
            if (ph == null || ph.posts.Count == 0)
            {
                Say("no post near this home.");
                return;
            }
            var options = new List<FloatMenuOption>();
            foreach (var p in ph.posts.ToList())
            {
                var post = p;
                var town = TownOf(post);
                options.Add(new FloatMenuOption((post.name ?? "?") + " (" + (post.people?.Name ?? "?") + ")", town == null ? (System.Action)null : () => act(home, ph, post, town)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Trading post: the keeper's warning now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWarn() => DebugPick((home, ph, p, town) =>
        {
            int now = Now;
            p.sourSince = now - TradingPostMath.WarnDays * Day;
            Warn(home, p, town, now);
            Say("warned. It closes after ten more days while their goodwill stays under 0; it mends at 0 or more.");
        });

        [DebugAction("Rimfolk", "Trading post: sack it now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSack() => DebugPick((home, ph, p, town) =>
        {
            var enemy = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f != null && f != p.people && !f.IsPlayer && !f.Hidden && f.def != null
                                                                                       && f.def.humanlikeFaction && f.HostileTo(p.people));
            if (enemy == null)
            {
                Say("no people is at war with " + (p.people?.Name ?? "them") + ".");
                return;
            }
            Sack(home, ph, p, town, enemy, Now);
        });

        [DebugAction("Rimfolk", "Trading post: close it now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugClose() => DebugPick((home, ph, p, town) =>
        {
            var options = new List<FloatMenuOption>();
            foreach (var e in new[] { PostEnd.Soured, PostEnd.War, PostEnd.Gone })
            {
                var end = e;
                options.Add(new FloatMenuOption(end.ToString(), () =>
                {
                    if (end == PostEnd.Gone)
                    {
                        End(home, ph, p, PostEnd.Gone, null, null);
                        return;
                    }
                    Close(home, ph, p, town, end, null);
                    if (ph.posts.Contains(p)) Say("not safe to take off the map now (a caravan, pods, a quest or a map there): it closes once it is.");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        });
    }
}
