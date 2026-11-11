using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods on the map in This week (UI/Inbox.cs), under Under way, in words and never with a time left: the stranger
    /// at the gate or staying (the question itself is the event's own letter, in Needs you), and the beast on the land
    /// (veiled as anything else until the colony is sure). What is coming is never shown: only the omens say so.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GodsOnMapPanels
    {
        static GodsOnMapPanels()
        {
            Inbox.Register("gods-on-map", InboxSection.UnderWay, 58f, Rows);
        }

        private static void Rows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null || !Gods.On) return;
            var b = GodsOnMapBook.Instance;
            if (b == null) return;
            try
            {
                var s = b.stranger;
                if (s?.x != null && s.x.Spawned && s.phase != StrangerPhase.WalkingOff && s.phase != StrangerPhase.Vanishing)
                {
                    var x = s.x;
                    rows.Add(new WeekRow
                    {
                        text = WeekModel.AtHome(s.map, GodStranger.StayLine(s)), pawn = x, sub = "CC_GodM_Week_StrangerSub".Translate().Resolve(),
                        click = WeekModel.Go(new GlobalTargetInfo(x)), follow = ThingRefs.Of(x), order = 1f
                    });
                }
                var a = b.beast?.a;
                if (a != null && a.Spawned && !a.Dead)
                {
                    bool veiled = GodsOnMapMath.Show(Gods.Realness) != GodShow.Plain;
                    var god = b.beast.God;
                    string line = (veiled ? "CC_GodM_Week_AnimalVeiled" : "CC_GodM_Week_Animal").Translate(GodsOnMapWords.Args(god, !veiled).Concat(new[] { a.def.label.Named("KIND") }).ToArray())
                        .Resolve();
                    rows.Add(new WeekRow
                    {
                        text = WeekModel.AtHome(b.beast.map, line.CapitalizeFirst()), pawn = a, sub = "CC_GodM_Week_AnimalSub".Translate().Resolve(),
                        click = WeekModel.Go(new GlobalTargetInfo(a)), follow = ThingRefs.Of(a), order = 2f
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods on the map: this week", e);
            }
        }
    }
}
