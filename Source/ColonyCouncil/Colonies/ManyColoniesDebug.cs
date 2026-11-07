using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: many colonies in play (Debug actions, "Rimfolk"): a colony's people welcomed here as if their home had been
    /// given up (the waits skipped, the rest by the rules), each colony's character, the towns staffed near each home, and
    /// where the next world event would be placed.
    /// </summary>
    public static class ManyColoniesDebug
    {
        private static void Say(string text) => Messages.Message("Many colonies: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Many colonies: two councils become one here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMerge()
        {
            var here = Find.CurrentMap;
            if (!Homes.IsHome(here))
            {
                Say("look at one of the colony's homes first.");
                return;
            }
            var options = new List<FloatMenuOption>();
            foreach (var other in Homes.All)
            {
                if (other == here) continue;
                var m = other;
                options.Add(new FloatMenuOption(Homes.Name(m) + " (its people on this map come as newcomers)", () =>
                {
                    var people = here.mapPawns.FreeColonistsSpawned.Where(p => Homes.Instance?.LastKeyOf(p) == Homes.KeyOf(m) || Homes.LastHome(p) == m).ToList();
                    if (people.Count == 0) people = Homes.People(m).Where(p => p.Spawned && p.Map == here).ToList();
                    Say(people.Count == 0 ? "nobody of " + Homes.Name(m) + " is on this map: send some here first." : Merges.DebugMerge(here, Homes.KeyOf(m), Homes.Name(m), people));
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no other home)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Many colonies: each colony's character", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCharacters()
        {
            foreach (var home in Homes.All)
            {
                var ch = CharacterTracker.For(home);
                if (ch == null) continue;
                string ways = string.Join(", ", CharacterTracker.All.Select(w => w + " " + ch.Value(w).ToString("0")));
                string traits = string.Join(", ", ch.traits.Known.Select(t => t.label));
                Log.Message("[Rimfolk] " + Homes.Name(home) + (ch.IsSeat ? " (the seat)" : "") + ": " + ways + "; known for: " + (traits.NullOrEmpty() ? "nothing yet" : traits)
                            + "; walls enough to be walled: " + ColonyTraits.HasWalls(home));
            }
            Say("each colony's character is in the log.");
        }

        [DebugAction("Rimfolk", "Many colonies: the towns near each home", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTowns()
        {
            var book = NotableBook.Instance;
            var seat = Homes.Seat;
            if (book == null || seat == null)
            {
                Say("no notables or no seat.");
                return;
            }
            var seatTowns = book.Towns(seat);
            Log.Message("[Rimfolk] the seat " + Homes.Name(seat) + ": " + string.Join(", ", seatTowns.Select(s => s.Name)));
            foreach (var s in book.FurtherTowns(seat, seatTowns))
                Log.Message("[Rimfolk] a further home's town: " + s.Name + " (" + s.Faction?.Name + "), notables: " + book.notables.Count(n => n.town == s.ID && n.Alive));
            Say("the towns staffed near each home are in the log.");
        }

        [DebugAction("Rimfolk", "Many colonies: where a world event would be placed", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPlace()
        {
            var counts = new Dictionary<Map, int>();
            for (int i = 0; i < 200; i++)
            {
                var m = WorldSituations.PlaceNear();
                if (m != null) counts[m] = (counts.TryGetValue(m, out int n) ? n : 0) + 1;
            }
            Say(string.Join(", ", counts.Select(kv => Homes.Name(kv.Key) + " " + kv.Value + " of 200 (" + Homes.People(kv.Key).Count + " people)")));
        }
    }
}
