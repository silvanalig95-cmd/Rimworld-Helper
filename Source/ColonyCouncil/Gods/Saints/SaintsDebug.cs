using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Dev actions for the saints ("Gods: ..."), in the dev menu's Rimfolk section. Dev mode only.</summary>
    public static class SaintsDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: a faith's saints (log)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Faith(Pawn p)
        {
            if (p?.Ideo == null)
            {
                Say("no faith (Ideology)");
                return;
            }
            var sb = new StringBuilder("Saints of " + p.Ideo.name + " (" + Saints.HeaderWord(p.Ideo) + ", age " + Saints.AgeOf(p.Ideo) + "):\n");
            foreach (var s in Saints.Of(p.Ideo))
                sb.AppendLine($"  {Saints.Title(s)} ({s.type}): {(s.damned ? "damned" : "holy")}, of {s.god?.Key ?? "none"}{(Saints.OfGod(s.god) == null ? " (not known here)" : "")}");
            foreach (var s in Saints.Book?.saints ?? new System.Collections.Generic.List<ColonySaint>())
                sb.AppendLine($"  the colony's: {Saints.Title(s)}, {s.reason}, of {Gods.ById(s.god)?.Key ?? "none"}, day {s.feastDay}, named year {s.namedYear}, kept {s.lastFeastYear}");
            Log.Message(sb.ToString());
            Say("written to the log");
        }

        /// <summary>The fallen colonist lying here (a corpse, or in a grave), or null.</summary>
        private static Pawn DeadAt(Map map, IntVec3 c)
        {
            foreach (var t in c.GetThingList(map))
            {
                if (t is Corpse corpse && corpse.InnerPawn?.Faction == Faction.OfPlayer) return corpse.InnerPawn;
                if (t is Building_Grave g && g.HasCorpse && g.Corpse.InnerPawn?.Faction == Faction.OfPlayer) return g.Corpse.InnerPawn;
            }
            return null;
        }

        [DebugAction("Rimfolk", "Gods: a fallen colonist waits for the saint's card", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Wait()
        {
            var map = Find.CurrentMap;
            var dead = DeadAt(map, UI.MouseCell());
            var b = Saints.Book;
            if (dead == null || b == null)
            {
                Say("click a colonist's corpse or grave");
                return;
            }
            var g = PawnGods.First(dead) ?? Worship.GodOf(dead, GodDomain.Healing) ?? Gods.All.FirstOrDefault(x => Gods.Known(x) && x.kind == GodKind.Ordinary);
            if (g == null)
            {
                Say("no god the colony knows");
                return;
            }
            b.waiting.RemoveAll(x => x.pawnId == dead.ThingID);
            b.waiting.Add(new SaintCandidate
            {
                pawnId = dead.ThingID, name = dead.LabelShort, fullName = dead.Name?.ToStringFull ?? dead.LabelShort, gender = dead.gender, god = g.id,
                ideo = ModsConfig.IdeologyActive ? dead.Ideo : null, reason = SaintReason.Healer, died = Find.TickManager.TicksGame, feastDay = GenLocalDate.DayOfYear(map),
                home = Gods.HomeKey(map)
            });
            Say(dead.LabelShort + " waits for the council's card (as a healer, of " + g.Key + "); ask it with \"Gods: the saint's card now\"");
        }

        [DebugAction("Rimfolk", "Gods: the saint's card now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Card()
        {
            var map = Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;
            var tracker = DilemmaTracker.For(map) ?? CouncilGameComponent.Instance?.dilemmas;
            bool fired = tracker != null && tracker.TryFire(true, d => d.Worker is DilemmaWorker_Saint);
            Say(fired ? "the council is asked." : "nobody waits (a remarkable death, at this home, in the last twelve days), or nobody to ask, or a question already waits.");
        }

        [DebugAction("Rimfolk", "Gods: a saint's day kept now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Feast()
        {
            var map = Find.CurrentMap;
            var s = Saints.Book?.saints.LastOrDefault(x => x != null && x.home == Gods.HomeKey(map));
            if (s == null)
            {
                Say("no saint of the colony's at this home");
                return;
            }
            Say(SaintsPlaces.Keep(s, map, Find.TickManager.TicksGame) ? Saints.Title(s) + "'s day is kept" : "nobody devoted to " + Saints.Title(s) + " here");
        }

        [DebugAction("Rimfolk", "Gods: pray at a saint's place now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Pray(Pawn p)
        {
            if (!SaintsPlaces.DevPray(p)) Say("no saint's place they can reach (a saint's grave, a statue raised to a saint, a relic)");
        }
    }
}
