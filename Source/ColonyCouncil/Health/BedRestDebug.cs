using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: bed rest only when it makes sense (Debug actions, "Rimfolk", "Bed rest: ..."). Why someone rests or keeps
    /// working, condition by condition; and a test patient with the player's case (small things the game would keep them in
    /// bed for) to watch them work on.
    /// </summary>
    public static class BedRestDebug
    {
        private static void Say(string text) => Messages.Message("Bed rest: " + text, MessageTypeDefOf.SilentInput, false);

        private static List<Pawn> Patients => Find.CurrentMap?.mapPawns.AllPawnsSpawned
            .Where(p => !p.Dead && p.RaceProps.IsFlesh && p.health.hediffSet.hediffs.Any(BedRest.Considered)).ToList() ?? new List<Pawn>();

        private static void Menu(IEnumerable<Pawn> pawns, System.Action<Pawn> act)
        {
            var list = pawns.Select(p => new FloatMenuOption(p.LabelShort + (BedRest.Why(p, out _) == BedRestMath.InBed.No ? " (keeps working)" : " (in bed)"), () => act(p))).ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption("(nobody here with anything to rest for)", null));
            Find.WindowStack.Add(new FloatMenu(list));
        }

        [DebugAction("Rimfolk", "Bed rest: why each one rests or works (log)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugExplain() => Menu(Patients, p =>
        {
            Log.Message("[Rimfolk] Bed rest only when it makes sense:\n" + BedRest.Explain(p));
            Say(p.LabelShort + ": what each condition says of bed rest is in the log.");
        });

        [DebugAction("Rimfolk", "Bed rest: a test patient (a head cold, a sunburn, a tended cut and bruise)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPatient() => Menu(Find.CurrentMap?.mapPawns.FreeColonistsSpawned.ToList() ?? new List<Pawn>(), p =>
        {
            var parts = p.health.hediffSet.GetNotMissingParts().ToList();
            BodyPartRecord Part(string defName) => parts.FirstOrDefault(r => r.def.defName == defName) ?? parts.FirstOrDefault(r => r.depth == BodyPartDepth.Outside);
            void Add(string def, float severity, string part = null, bool tend = false)
            {
                var d = DefDatabase<HediffDef>.GetNamedSilentFail(def);
                if (d == null) return;
                var r = part != null ? Part(part) : null;
                if (part != null && r == null) return;
                var h = HediffMaker.MakeHediff(d, p, r);
                h.Severity = severity;
                p.health.AddHediff(h, r);
                if (tend && h.TendableNow()) h.Tended(0.7f, 1f);
            }
            Add("Council_Ail_HeadCold", 0.45f);
            Add("Council_Ail_Sunburn", 0.4f);
            Add("Cut", 4f, "Hand", tend: true);
            Add("Bruise", 3f, "Leg", tend: true);
            Log.Message("[Rimfolk] Bed rest only when it makes sense, the test patient:\n" + BedRest.Explain(p));
            Say(p.LabelShort + " has a head cold, a sunburn, a tended cut and a tended bruise; the game would keep them in bed, the rule keeps them working (the log has why). Add the flu with the game's own Add hediff to see them go to bed.");
        });
    }
}
