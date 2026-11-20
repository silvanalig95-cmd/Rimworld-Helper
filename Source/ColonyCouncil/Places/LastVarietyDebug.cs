using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode (Debug actions, "Rimfolk", "Last variety: ..."): a day off for a friend asked now (A9), and a place named at
    /// the mouse as each kind would name it, the names listed, a place's thing taken as gone, supper talk now (C25).
    /// </summary>
    public static class LastVarietyDebug
    {
        private static void Say(string text) => Messages.Message("Last variety: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Menu(List<FloatMenuOption> options, string empty)
        {
            if (options.Count == 0) options.Add(new FloatMenuOption(empty, null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        // ---------------- A9: a day off for a friend ----------------

        [DebugAction("Rimfolk", "Last variety: a day off for a friend, asked now by...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FriendDayOff()
        {
            var comp = CouncilGameComponent.Instance;
            var def = DefDatabase<WishDef>.GetNamedSilentFail(WishWorker_FriendDayOff.DefName);
            var map = Find.CurrentMap;
            if (comp == null || def == null || map == null) return;
            var options = map.mapPawns.FreeColonistsSpawned.Where(p => p.DevelopmentalStage.Adult()).Select(p =>
            {
                var asker = p;
                return new FloatMenuOption(asker.LabelShort, () =>
                {
                    if (comp.OpenWishBy(asker) is Wish open) comp.Resolve(open, WishState.Cancelled);
                    var c = def.Worker.CandidatesFor(asker).FirstOrDefault();
                    if (c == null)
                    {
                        Say(asker.LabelShort + " can't ask: nobody at home worn down two days whom they are close to (or, kind, don't dislike), with a day off ready to give, outside worn thin and the step-down question.");
                        return;
                    }
                    comp.CreateWish(def, asker, asker, c);
                });
            }).ToList();
            Menu(options, "(nobody at home)");
        }

        // ---------------- C25: the colony's names for its places ----------------

        [DebugAction("Rimfolk", "Last variety: name the place at the mouse as...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void NameHere()
        {
            var map = Find.CurrentMap;
            var cell = UI.MouseCell();
            if (map == null || !cell.InBounds(map)) return;
            var options = PlaceNameMath.Kinds.Select(k =>
            {
                string kind = k;
                return new FloatMenuOption(kind, () =>
                {
                    var p = PlaceNames.DebugName(map, cell, kind);
                    Say(p != null ? "named " + p.name + " by " + p.namer + "." : "not named (the setting off, nobody to name it, too near another place, a name already given, or twelve already).");
                });
            }).ToList();
            Menu(options, "(no kinds)");
        }

        [DebugAction("Rimfolk", "Last variety: list the named places here", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void List()
        {
            var book = PlaceBook.Of(Find.CurrentMap);
            if (book == null || book.places.Count == 0)
            {
                Say("no named places here.");
                return;
            }
            Say(string.Join("; ", book.places.Select(p => p.name + " (" + p.kind + ", " + p.cell + ", by " + p.namer + (p.Stands ? "" : ", gone") + ")")));
        }

        [DebugAction("Rimfolk", "Last variety: the place at the mouse gone a season ago", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GoneHere()
        {
            var map = Find.CurrentMap;
            var p = PlaceNames.Near(map, UI.MouseCell());
            if (p == null)
            {
                Say("no named place at the mouse.");
                return;
            }
            p.goneTick = Find.TickManager.TicksGame - (int)(PlaceNameMath.FadeDays * GenDate.TicksPerDay);
            Say(p.name + " is taken as gone a season ago: the next hour forgets it.");
        }

        [DebugAction("Rimfolk", "Last variety: a named place brought up at supper now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Supper()
        {
            var book = PlaceBook.Of(Find.CurrentMap);
            if (book == null) return;
            book.lastSupper = -1;
            PlaceTalk.Supper(Find.CurrentMap, book);
            Say(book.lastSupper >= 0 ? "said." : "nothing said (not an evening hour, nobody eating beside someone, the chance not met, or no named place).");
        }
    }
}
