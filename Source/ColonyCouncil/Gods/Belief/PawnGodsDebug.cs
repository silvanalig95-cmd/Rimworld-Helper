using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Dev actions for each colonist's own gods ("Gods: ..."), in the dev menu's Rimfolk section. Dev mode only: the numbers are never shown in play.</summary>
    public static class PawnGodsDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Gods: a colonist's own gods (log)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Show(Pawn p)
        {
            var r = PawnGods.Of(p);
            if (r == null)
            {
                Say("not one of the colony's people");
                return;
            }
            var sb = new StringBuilder("Gods: " + p.LabelShort + (Worship.Scoffer(p) ? " (a scoffer)" : "") + (r.wonOver >= 0 ? " (won over)" : "") + "\n");
            foreach (var h in r.gods)
            {
                var g = Gods.ById(h.god);
                sb.AppendLine($"  {g?.Key ?? "?"}: hold {h.hold:0.0} (drawn {h.drawn:0.0}), dread {h.dread:0.0}, {PawnGodsMath.Word(h.hold, h.dread)}{(Gods.Known(g) ? "" : ", not heard of")}; {h.why} {h.dreadWhy}");
            }
            sb.AppendLine("  prays to: " + (Worship.GodOf(p)?.Key ?? "nobody"));
            Log.Message(sb.ToString());
            Say("written to the log");
        }

        [DebugAction("Rimfolk", "Gods: set a colonist's belief...", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Set(Pawn p)
        {
            if (PawnGods.Of(p) == null) return;
            var opts = new List<DebugMenuOption>();
            foreach (var g in Gods.All.Where(x => x.kind != GodKind.Dead).ToList())
            {
                var god = g;
                opts.Add(new DebugMenuOption(god.Key, DebugMenuOptionMode.Action, () =>
                {
                    var levels = new List<DebugMenuOption>();
                    foreach (var (label, hold, dread) in new[] { ("leans", 15f, 0f), ("holds", 40f, 0f), ("devoted", 65f, 0f), ("fervent", 90f, 0f), ("in dread", 20f, 60f), ("none", 0f, 0f) })
                        levels.Add(new DebugMenuOption(label, DebugMenuOptionMode.Action, () => PawnGods.DevSet(p, god, hold, dread)));
                    Find.WindowStack.Add(new Dialog_DebugOptionListLister(levels));
                }));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(opts));
        }

        [DebugAction("Rimfolk", "Gods: their small rite now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Rite(Pawn p)
        {
            if (!PawnGodsActs.DevRite(p)) Say("no god of theirs, or nowhere near to keep its rite");
        }

        [DebugAction("Rimfolk", "Gods: win them over now...", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void WinOver(Pawn p)
        {
            if (PawnGods.Of(p) == null) return;
            var opts = new List<DebugMenuOption>();
            foreach (var g in Gods.All.Where(x => x.kind != GodKind.Dead && Gods.Known(x)).ToList())
            {
                var god = g;
                opts.Add(new DebugMenuOption(god.Key, DebugMenuOptionMode.Action,
                    () => PawnGodsActs.MaybeWonOver(p, god, BeliefEvent.SawPlain, "CC_Belief_Why_Saw".Translate(GodLorePage.Date(Find.TickManager.TicksGame).Named("DATE"), "(dev)".Named("WHAT")).Resolve(), true)));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(opts));
        }

        [DebugAction("Rimfolk", "Gods: rival devotees bicker now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Bicker(Pawn p)
        {
            var other = p?.Map?.mapPawns.FreeColonistsSpawned.Where(x => x != p && Together.Can(p, x)).OrderByDescending(x => PawnGodsActs.BickerWeight(p, x)).FirstOrDefault();
            var def = DefDatabase<InteractionDef>.GetNamedSilentFail("Rimfolk_Belief_RivalGods");
            if (other == null || def == null || PawnGodsActs.BickerWeight(p, other) <= 0f)
            {
                Say("nobody near who holds a god set against theirs");
                return;
            }
            // together: Together.Can found them side by side.
            p.interactions?.TryInteractWith(other, def);
        }
    }
}
