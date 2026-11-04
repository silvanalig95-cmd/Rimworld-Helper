using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where the colony sees its trading posts (World/Region/TradingPost.cs): Region → Map's Trade lens (each post on the
    /// map with its line to its town, and as a list: whose, where, since when, its keeper, a warning given, how the last
    /// ones went; UI/Region/LensModel.cs), a post's card (TradingPostWords' lines), and a line on This week while a post's
    /// keeper has warned it will close. Its old page name, "TradingPosts", forwards to the Trade lens's list.
    /// </summary>
    public static class TradingPostPage
    {
        public const string Name = "TradingPosts";

        // ---------------- This week ----------------

        /// <summary>A line for each post whose keeper has warned it will close unless things mend (UI/WeekModel.cs): no timer, what would mend it.</summary>
        public static void WeekRows(WeekCard card)
        {
            if (card == null || RegionBook.Instance == null || !TradingPosts.On) return;
            foreach (var (home, p) in TradingPosts.All())
            {
                if (p.warnedAt < 0 && p.closing < 0) continue;
                var town = TradingPosts.TownOf(p);
                string text = "CC_PostPg_Week".Translate((p.name ?? "?").Named("POST"), (p.people?.Name ?? "?").Named("PEOPLE")).Resolve();
                if (Homes.All.Count > 1) text = Homes.Name(home) + ": " + text;
                card.rows.Add(new WeekRow
                {
                    icon = Icons.Trader,
                    color = UiKit.Warn,
                    text = text,
                    note = (p.closing >= 0 ? "CC_PostPg_WeekClosing" : "CC_PostPg_WeekWarned").Translate().Resolve(),
                    noteColor = UiKit.Warn,
                    tip = string.Join("\n", TradingPostWords.Lines(home, p, false)),
                    click = town != null ? WeekModel.Go(new GlobalTargetInfo(town)) : WeekModel.Page(Name),
                    order = -1
                });
            }
        }
    }
}
