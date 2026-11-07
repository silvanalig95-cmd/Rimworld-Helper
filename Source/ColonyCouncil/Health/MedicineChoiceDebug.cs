using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: medicine by what is treated (Debug actions, "Rimfolk", "Medicine by need: ..."). What each condition gets
    /// and why, the tendings in the game's order; and a test patient with the player's case (a bruise and scratches beside
    /// an illness) to watch a doctor tend.
    /// </summary>
    public static class MedicineChoiceDebug
    {
        private static void Say(string text) => Messages.Message("Medicine by need: " + text, MessageTypeDefOf.SilentInput, false);

        private static List<Pawn> Patients => Find.CurrentMap?.mapPawns.AllPawnsSpawned.Where(MedicineChoice.Cared).ToList() ?? new List<Pawn>();

        private static void Menu(IEnumerable<Pawn> pawns, System.Action<Pawn> act)
        {
            var list = pawns.Select(p => new FloatMenuOption(p.LabelShort + " (" + ((MedicalCareCategory)MedicineChoice.Ceiling(p)).GetLabel() + ")", () => act(p))).ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption("(nobody here the colony tends)", null));
            Find.WindowStack.Add(new FloatMenu(list));
        }

        [DebugAction("Rimfolk", "Medicine by need: what each condition gets (log)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugExplain() => Menu(Patients.Where(p => p.health.hediffSet.hediffs.Any(h => h.TendableNow(ignoreTimer: true))), p =>
        {
            Log.Message("[Rimfolk] Medicine by what is treated:\n" + MedicineChoice.Explain(p));
            Say(p.LabelShort + ": what each condition gets, and the tendings in order, are in the log.");
        });

        [DebugAction("Rimfolk", "Medicine by need: a test patient (a bruise, scratches, a cut and the flu)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugPatient() => Menu(Patients.Where(p => p.RaceProps.Humanlike), p =>
        {
            var parts = p.health.hediffSet.GetNotMissingParts().ToList();
            BodyPartRecord Part(string defName) => parts.FirstOrDefault(r => r.def.defName == defName) ?? parts.FirstOrDefault(r => r.depth == BodyPartDepth.Outside);
            void Wound(string def, string part, float severity)
            {
                var d = DefDatabase<HediffDef>.GetNamedSilentFail(def);
                var r = Part(part);
                if (d == null || r == null) return;
                var h = HediffMaker.MakeHediff(d, p, r);
                h.Severity = severity;
                p.health.AddHediff(h, r);
            }
            Wound("Bruise", "Leg", 4f);
            Wound("Scratch", "Arm", 1f);
            Wound("Scratch", "Hand", 4f);
            Wound("Cut", "Shoulder", 3f);
            var flu = DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
            if (flu != null && !p.health.hediffSet.HasHediff(flu)) p.health.AddHediff(flu);
            Log.Message("[Rimfolk] Medicine by what is treated, the test patient:\n" + MedicineChoice.Explain(p));
            Say(p.LabelShort + " has a bruise, two scratches, a cut and the flu; put them in bed and watch the doctor (what each gets is in the log).");
        });
    }
}
