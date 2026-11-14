using System;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: each god's own presence (Debug actions, "Rimfolk", "Gods: ..."): begin one here for any god and kind of
    /// moment, as the colony knows the god or named for testing; end it; see what it changes (log); and have every offering
    /// waiting at the map's shrines taken now, each in its god's own way.
    /// </summary>
    public static class GodPresenceOwnDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        private static readonly PresenceKind[] Kinds = { PresenceKind.Blessing, PresenceKind.Appear, PresenceKind.Act, PresenceKind.Depart, PresenceKind.Wrath };

        [DebugAction("Rimfolk", "Gods: a god's own presence here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBegin()
        {
            var map = Find.CurrentMap;
            var gods = Gods.All.Where(g => g != null).ToList();
            if (map == null || gods.Count == 0)
            {
                Say("no gods in this game.");
                return;
            }
            Find.WindowStack.Add(new FloatMenu(gods.Select(g => new FloatMenuOption(Gods.Name(g) + (GodPresenceOwn.DefOf(g) == null ? " (the shared presence)" : ""), () =>
                Find.WindowStack.Add(new FloatMenu(Kinds.SelectMany(k => new[] { false, true }.Select(named => new FloatMenuOption(k + (named ? ", named (testing)" : ", as the colony knows it"), () =>
                {
                    string known = named ? Gods.Name(g) : Gods.CardName(g, map);
                    var look = GodsWiring.LookOf(g);
                    GameCondition_GodPresence.Begin(map, k, g.colour, look?.still ?? 0.5f, known ?? GodLore.LabelFor(Gods.Name(g)), known == null, GodPresenceOwn.DefOf(g), g.Key);
                    GodPresenceOwn.Forget(map);
                    var c = map.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>();
                    Say(c == null ? "it couldn't begin." : c.LabelCap + ", for " + c.TicksLeft.ToStringTicksToPeriod() + ".");
                }))).ToList())))).ToList()));
        }

        [DebugAction("Rimfolk", "Gods: end the presence here", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEnd()
        {
            var c = Find.CurrentMap?.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>();
            if (c == null)
            {
                Say("no presence here.");
                return;
            }
            c.End();
            Say("ended.");
        }

        [DebugAction("Rimfolk", "Gods: the presence here (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLog()
        {
            var c = Find.CurrentMap?.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>();
            if (c == null)
            {
                Log.Message("[Rimfolk] No god's presence on this map.");
                return;
            }
            Log.Message("[Rimfolk] " + c.def.defName + " (" + c.Effect + ", " + c.kind + ", strength " + c.Strength.ToString("F2") + (c.Harsh ? ", a wrath" : "") + "): " + c.LabelCap
                        + ", " + c.TicksLeft.ToStringTicksToPeriod() + " left\n" + c.Description
                        + (c.prankPawn != null ? "\nPip's prank: " + c.prankPawn.LabelShort + " at " + c.prankDoor?.Position : ""));
            Say("logged.");
        }

        [DebugAction("Rimfolk", "Gods: offerings here taken now, each god's own way", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTake()
        {
            var b = Worship.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            int n = 0;
            foreach (var o in b.offerings.Where(x => x != null && x.set && x.fate == OfferingFate.Waiting && x.home == Gods.HomeKey(map) && x.thing != null && x.thing.Spawned).ToList())
            {
                try
                {
                    Offerings.TakeNow(b, o, map);
                    n++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: an offering taken by hand", e);
                }
            }
            Say(n == 0 ? "no offering waits at a shrine here (set one down first: offerings at every shrine now)." : n + " taken or let go.");
        }
    }
}
