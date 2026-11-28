using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the living land (Debug actions, "Rimfolk", "Land: ..."): what the land's record holds, each of its four
    /// parts now, a sickness of the wild plants begun now and run on a few days, the patches a stage on, a dry or a wet spell's
    /// water now and the water put back, a pasture worn and rested.
    /// </summary>
    public static class LandDebug
    {
        private static void Say(string text) => Messages.Message("Land: " + text, MessageTypeDefOf.SilentInput, false);

        private static LandBook Book => LandBook.Of(Find.CurrentMap);

        [DebugAction("Rimfolk", "Land: what the land's record holds (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRecord()
        {
            var b = Book;
            if (b == null) return;
            Log.Message("[Rimfolk] The land's record here:\n" + b.Describe());
            Say("written to the log.");
        }

        [DebugAction("Rimfolk", "Land: each of the land's four parts now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugParts()
        {
            var b = Book;
            if (b == null) return;
            for (int part = 0; part < LandMath.Parts; part++) b.Run(part);
            Say(b.Describe().Replace("\n", " | "));
        }

        [DebugAction("Rimfolk", "Land: a sickness of the wild plants now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSick()
        {
            var map = Find.CurrentMap;
            Find.WindowStack.Add(new FloatMenu(new[] { LandSickKind.Wilt, LandSickKind.Rust, LandSickKind.Rot }
                .Select(k => new FloatMenuOption(k.ToString().ToLowerInvariant(), () => Say(LandSickness.DevStart(map, (int)k)))).ToList()));
        }

        [DebugAction("Rimfolk", "Land: the sickness three days on", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSickDays() => Say(LandSickness.DevDays(Find.CurrentMap, 3));

        [DebugAction("Rimfolk", "Land: the council's question on the sickness now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAsk()
        {
            var map = Find.CurrentMap;
            var o = LandBook.Of(map)?.Outbreak;
            if (o == null)
            {
                Say("no sickness going on here.");
                return;
            }
            o.asked = false;
            var tracker = DilemmaTracker.For(map) ?? CouncilGameComponent.Instance?.dilemmas;
            Say(tracker != null && tracker.TryFire(true, d => d.defName == LandSickness.Question) ? "asked." : "couldn't be asked now (a question is waiting, too few sick, or nobody to ask).");
        }

        [DebugAction("Rimfolk", "Land: the burn the council chose, now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBurn()
        {
            var map = Find.CurrentMap;
            var b = LandBook.Of(map);
            if (b?.Outbreak == null)
            {
                Say("no sickness going on here.");
                return;
            }
            string why = LandSickness.CantBurn(map);
            if (why != null)
            {
                Say(why);
                return;
            }
            LandSickness.SetBurn(map, null);
            LandSickness.BurnHour(map, b);
            Say(b.Outbreak?.burnWaiting == true ? "the burn waits (it is set in daylight, dry, with nobody fighting)." : "set going.");
        }

        [DebugAction("Rimfolk", "Land: every patch here a stage on", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStage() => Say(LandRegrowth.DevStage(Find.CurrentMap));

        [DebugAction("Rimfolk", "Land: a dry spell's water now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDry() => Say(LandWater.DevSpell(Find.CurrentMap, true));

        [DebugAction("Rimfolk", "Land: a wet spell's water now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWet() => Say(LandWater.DevSpell(Find.CurrentMap, false));

        [DebugAction("Rimfolk", "Land: the water put back as it was", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWaterBack() => Say(LandWater.DevRestoreAll(Find.CurrentMap));

        [DebugAction("Rimfolk", "Land: the busiest pasture worn now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWear() => Say(LandPasture.DevWear(Find.CurrentMap));

        [DebugAction("Rimfolk", "Land: every pasture rested and put back", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRest() => Say(LandPasture.DevRest(Find.CurrentMap));
    }
}
