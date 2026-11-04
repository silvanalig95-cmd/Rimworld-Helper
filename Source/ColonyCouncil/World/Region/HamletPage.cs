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

        /// <summary>A line while settlers wait at the gate (or on the road below it) for the colony's caravan (UI/WeekModel.cs): no timer, what takes them along.</summary>
        public static void WeekRows(WeekCard card)
        {
            if (card == null || RegionBook.Instance == null || !Hamlets.On) return;
            foreach (var (home, h) in Hamlets.All())
            {
                if (h.stage != HamletStage.Waiting) continue;
                string text = "CC_HamletPg_Week".Translate((h.people?.Name ?? "?").Named("PEOPLE")).Resolve();
                if (Homes.All.Count > 1) text = Homes.Name(home) + ": " + text;
                var leader = h.leader != null && h.leader.Spawned ? h.leader : null;
                var t = Hamlets.TileOf(h);
                card.rows.Add(new WeekRow
                {
                    icon = Icons.Colony,
                    color = UiKit.Good,
                    text = text,
                    note = "CC_HamletPg_WeekNote".Translate().Resolve(),
                    noteColor = UiKit.Good,
                    tip = string.Join("\n", HamletWords.Lines(home, h, false)),
                    click = leader != null ? WeekModel.Go(new GlobalTargetInfo(leader)) : t.Valid ? WeekModel.Go(new GlobalTargetInfo(t)) : WeekModel.Page(Name),
                    order = -1
                });
            }
        }
    }
}
