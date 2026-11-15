using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the gods' touch (Debug actions, "Rimfolk", "Gods: ..."): one of a god's effects now where the mouse is, any of
    /// its own by name, felt by everyone near it as if they had seen the god's occurrence there; and the touch's book (log).
    /// </summary>
    public static class GodTouchDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: a god's touch here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTouch()
        {
            var map = Find.CurrentMap;
            var gods = Gods.All.Where(g => g != null && g.kind != GodKind.Dead).ToList();
            if (map == null || gods.Count == 0)
            {
                Say("no gods in this game.");
                return;
            }
            var cell = UI.MouseCell();
            if (!cell.InBounds(map)) cell = map.Center;
            Find.WindowStack.Add(new FloatMenu(gods.Select(g => new FloatMenuOption(Gods.Name(g), () =>
            {
                var options = GodTouchMath.Of(g.Key).Select(e => new FloatMenuOption(e.ToString(), () =>
                {
                    GodTouch.DevNow(g, map, cell, e.dark ? TouchTone.Dark : TouchTone.Kind, e.tier, e.effect, e.tier == TouchTier.Great);
                    Say(Gods.Name(g) + ": " + e + " (" + (GodTouchEffects.Can(e, new TouchContext(g, map, cell, map.mapPawns.FreeColonistsSpawned.ToList(), g, false, e.tier, e.dark))
                        ? "could happen here" : "had nothing to do here, so another of the tier, if any") + ").");
                })).ToList();
                foreach (var t in new[] { TouchTier.Small, TouchTier.Notable, TouchTier.Great })
                {
                    var tier = t;
                    options.Add(new FloatMenuOption("any " + tier.ToString().ToLowerInvariant() + " kindness", () => GodTouch.DevNow(g, map, cell, TouchTone.Kind, tier, null, tier == TouchTier.Great)));
                }
                options.Add(new FloatMenuOption("an omen seen (as the rules have it, moods and maybe a small effect)", () => GodTouch.DevNow(g, map, cell, TouchTone.Sign, TouchTier.Small, null, false)));
                Find.WindowStack.Add(new FloatMenu(options));
            })).ToList()));
        }

        [DebugAction("Rimfolk", "Gods: the gods' touch (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugLog()
        {
            var b = GodTouchBook.Instance;
            if (b == null)
            {
                Say("no book.");
                return;
            }
            int now = Find.TickManager.TicksGame;
            var sb = new StringBuilder("The gods' touch:\n");
            foreach (var kv in b.last.OrderBy(k => k.Key)) sb.AppendLine("  " + kv.Key + ": " + ((now - kv.Value) / (float)GenDate.TicksPerDay).ToString("0.0") + " days ago");
            sb.AppendLine("Lately (home|god|effect|tick):");
            foreach (var r in b.recent) sb.AppendLine("  " + r);
            sb.AppendLine("Occurrences seen in the last " + GodTouchMath.TurnDays + " days, by god at each home (their own happenings that much less likely):");
            foreach (var home in Homes.All.Where(m => m != null))
            foreach (var g in Gods.All.Where(x => x != null))
            {
                int n = b.SeenOf(Gods.HomeKey(home), g.Key, now, GodTouchMath.TurnDays);
                if (n > 0) sb.AppendLine("  " + Homes.Name(home) + ", " + Gods.Name(g) + ": " + n + " (x" + GodTouchMath.Turn(n).ToString("0.00") + ")");
            }
            Log.Message(sb.ToString());
            Say("written to the log.");
        }
    }
}
