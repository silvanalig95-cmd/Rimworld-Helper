using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: casting on their own ("Rimfolk", "Magic: casting on their own ..."): for each colonist on this map who
    /// knows one of the damage spells used, whether they are switched on, their spells, and what they would cast now or
    /// why not (the rules only: nothing is cast).
    /// </summary>
    public static class MagicAutoCastDebug
    {
        [DebugAction("Rimfolk", "Magic: casting on their own, the colonists here (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCasters()
        {
            if (!PeoplesMagic.Loaded || !RomSpells.OwnReady)
            {
                Messages.Message("Casting on their own: A RimWorld of Magic isn't loaded, or its casting can't be read.", MessageTypeDefOf.SilentInput, false);
                return;
            }
            var book = MagicAutoCastBook.Instance;
            int now = Find.TickManager.TicksGame;
            var sb = new StringBuilder("[Rimfolk] Casting on their own (setting " + (MagicAutoCast.On ? "on" : "off") + "):");
            sb.AppendLine();
            int n = 0;
            foreach (var p in Find.CurrentMap.mapPawns.FreeColonistsSpawned)
            {
                var spells = RomSpells.HeldBy(p).Where(h => MagicAutoCastMath.Of(h.def.defName) != null).Select(h => h.def.defName).ToList();
                if (spells.Count == 0) continue;
                n++;
                MagicAutoCast.Consider(p, now, true, out string what);
                sb.AppendLine("    " + p.LabelShort + (book != null && book.IsOn(p.thingIDNumber) ? " (switched on)" : " (off)") + ": " + string.Join(", ", spells)
                              + "; mana " + Pool(p, false) + ", stamina " + Pool(p, true) + "; now: " + what);
            }
            if (n == 0) sb.AppendLine("    nobody here knows one of the damage spells used.");
            Log.Message(sb.ToString().TrimEnd());
            Messages.Message("Casting on their own: listed in the log.", MessageTypeDefOf.SilentInput, false);
        }

        private static string Pool(Pawn p, bool might)
        {
            var need = RomSpells.Pool(p, might);
            return need == null ? "none" : need.CurLevel.ToString("0.00") + "/" + need.MaxLevel.ToString("0.00");
        }
    }
}
