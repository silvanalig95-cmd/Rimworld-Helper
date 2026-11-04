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
    /// Where the colony sees its hamlets (World/Region/Hamlet.cs): Region → Map's Ours lens (each home's hamlet on the map
    /// with its line from home, and as a list with the settlers on their way: whose, where, since when, its head, its oath,
    /// what it asked lately, how the last ones went; UI/Region/LensModel.cs), a hamlet's card (HamletWords' lines), and a
    /// line on This week while settlers wait at the gate for the colony's caravan (never with a timer). Its old page name,
    /// "Hamlets", forwards to the Ours lens's list.
    /// </summary>
    public static class HamletPage
    {
        public const string Name = "Hamlets";

        /// <summary>The hamlet's name and people, or the settlers' people while it has no name yet.</summary>
        public static string Title(HamletHome h) =>
            HamletMath.Stands(h.stage) && !h.name.NullOrEmpty()
                ? "CC_HamletPg_Title".Translate(h.name.Named("HAMLET"), (h.people?.Name ?? "?").Named("PEOPLE")).Resolve()
                : "CC_HamletPg_TitleSettlers".Translate((h.people?.Name ?? "?").Named("PEOPLE")).Resolve();

        /// <summary>Where the settlers are on the land while they have no town: the caravan they go with, else their hamlet's place.</summary>
        public static GlobalTargetInfo Target(HamletHome h)
        {
            var town = Hamlets.TownOf(h);
            if (town != null) return new GlobalTargetInfo(town);
            var c = h.stage == HamletStage.Escorted ? h.escort?.GetCaravan() : null;
            if (c != null) return new GlobalTargetInfo(c);
            var t = Hamlets.TileOf(h);
            return t.Valid ? new GlobalTargetInfo(t) : GlobalTargetInfo.Invalid;
        }

        // ---------------- This week ----------------

        /// <summary>
        /// A line in This week's Needs you while settlers wait at the gate (or on the road below it) for the colony's
        /// caravan (UI/Inbox.cs): no timer, what takes them along, its act going to them. On the road, they are under way.
        /// </summary>
        public static void WeekRows(List<WeekRow> rows)
        {
            if (rows == null || RegionBook.Instance == null || !Hamlets.On) return;
            foreach (var (home, h) in Hamlets.All())
            {
                if (h.stage != HamletStage.Waiting) continue;
                string people = h.people != null ? ThingRefs.Link(h.people.Name, ThingRefs.Of(h.people)) : "?";
                string text = "CC_HamletPg_Week".Translate(people.Named("PEOPLE")).Resolve();
                if (Homes.All.Count > 1) text = Homes.Name(home) + ": " + text;
                var leader = h.leader != null && h.leader.Spawned ? h.leader : null;
                var t = Hamlets.TileOf(h);
                var go = leader != null ? WeekModel.Go(new GlobalTargetInfo(leader)) : t.Valid ? WeekModel.Go(new GlobalTargetInfo(t)) : WeekModel.Page(Name);
                rows.Add(new WeekRow
                {
                    icon = Icons.Colony,
                    color = UiKit.Good,
                    faction = h.people,
                    text = text,
                    sub = "CC_HamletPg_WeekNote".Translate().Resolve(),
                    subColor = UiKit.Good,
                    tip = string.Join("\n", HamletWords.Lines(home, h, false)),
                    click = go,
                    act = InboxWords.Look,
                    actRun = go,
                    order = -1
                });
            }
        }

        /// <summary>How many settlers' parties wait at the gate (the one count of what waits on the player).</summary>
        public static int WeekCount()
        {
            if (RegionBook.Instance == null || !Hamlets.On) return 0;
            int n = 0;
            foreach (var (_, h) in Hamlets.All())
                if (h.stage == HamletStage.Waiting)
                    n++;
            return n;
        }
    }
}
