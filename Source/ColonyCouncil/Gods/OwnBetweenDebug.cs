using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the gods between the colony's people now (Debug actions, "Rimfolk", "Gods: &lt;god&gt;'s ... now"), one a
    /// happening (Gods/OnMap/Between/), kept out of Gods/OnMap as the others' are. Each brings its happening about at the map
    /// shown, past its pace, with whoever fits best (else the first who can), and says what came of it.
    /// </summary>
    public static class OwnBetweenDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Fire(OwnHappening h)
        {
            var b = OwnHappenings.Book;
            var map = Find.CurrentMap;
            if (b == null || map == null) return;
            var k = OwnBetween.KindOf(h);
            var g = k?.God;
            if (k == null || g == null)
            {
                Say("that god isn't in this world's pantheon.");
                return;
            }
            OwnHappenings.forced = h;
            try
            {
                b.between.scenes.RemoveAll(s => s.map == map);
                Say(k.Fire(b, g, map));
                b.Due(Find.TickManager.TicksGame + 1);
            }
            finally
            {
                OwnHappenings.forced = null;
            }
        }

        [DebugAction("Rimfolk", "Gods: Hollin's saplings between two who fought now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HollinBound() => Fire(OwnHappening.HollinBound);

        [DebugAction("Rimfolk", "Gods: Hollin's brambles round a woodcutter's stump now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HollinStumps() => Fire(OwnHappening.HollinStumps);

        [DebugAction("Rimfolk", "Gods: Smoulder's forge flaring at a fine piece now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SmoulderPraise() => Fire(OwnHappening.SmoulderPraise);

        [DebugAction("Rimfolk", "Gods: Smoulder's stones from the rock, one pulled back, now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SmoulderNearMiss() => Fire(OwnHappening.SmoulderNearMiss);

        [DebugAction("Rimfolk", "Gods: Bellwether's old one and child at the edge now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BellwetherTold() => Fire(OwnHappening.BellwetherTold);

        [DebugAction("Rimfolk", "Gods: Bellwether's hunter who let it go now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BellwetherSpared() => Fire(OwnHappening.BellwetherSpared);

        [DebugAction("Rimfolk", "Gods: Morel's food gone to rot, whose it was, now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MorelWasted() => Fire(OwnHappening.MorelWasted);

        [DebugAction("Rimfolk", "Gods: Morel's threads on the sealed stone now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MorelSealed() => Fire(OwnHappening.MorelSealed);

        [DebugAction("Rimfolk", "Gods: Ennu's bowl for the newcomer now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EnnuWelcomed() => Fire(OwnHappening.EnnuWelcomed);

        [DebugAction("Rimfolk", "Gods: Ennu's waiting up for the late one now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void EnnuWaitedUp() => Fire(OwnHappening.EnnuWaitedUp);

        [DebugAction("Rimfolk", "Gods: Pip's machine mended by two hands now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PipTwoHands() => Fire(OwnHappening.PipTwoHands);

        [DebugAction("Rimfolk", "Gods: Pip's machine that hums for its talker now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PipTalked() => Fire(OwnHappening.PipTalked);

        [DebugAction("Rimfolk", "Gods: Cirrus's star for two watchers now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CirrusTwoWatchers() => Fire(OwnHappening.CirrusTwoWatchers);

        [DebugAction("Rimfolk", "Gods: Cirrus's shapes in the clouds now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CirrusCloudShape() => Fire(OwnHappening.CirrusCloudShape);

        [DebugAction("Rimfolk", "Gods: Hush's coat for the cold one now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HushCoat() => Fire(OwnHappening.HushCoat);

        [DebugAction("Rimfolk", "Gods: Hush's quiet asked on the cold night now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HushNoise() => Fire(OwnHappening.HushNoise);

        [DebugAction("Rimfolk", "Gods: Undertow's whisperer seen at the edge now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void UndertowWhisper() => Fire(OwnHappening.UndertowWhisper);

        [DebugAction("Rimfolk", "Gods: Undertow's light let go out now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void UndertowLampOut() => Fire(OwnHappening.UndertowLampOut);

        [DebugAction("Rimfolk", "Gods: Helios's walk out into the sun now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HeliosSun() => Fire(OwnHappening.HeliosSun);

        [DebugAction("Rimfolk", "Gods: Helios's light on two at odds now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HeliosAlike() => Fire(OwnHappening.HeliosAlike);

        [DebugAction("Rimfolk", "Gods: Helios's call into the shade now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HeliosShade() => Fire(OwnHappening.HeliosShade);

        [DebugAction("Rimfolk", "Gods: Selene's sorry after the moonlit night now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SeleneSorry() => Fire(OwnHappening.SeleneSorry);

        [DebugAction("Rimfolk", "Gods: Selene's same dream at breakfast now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SeleneSameDream() => Fire(OwnHappening.SeleneSameDream);

        [DebugAction("Rimfolk", "Gods: Tamiu's cat on the grave now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MogGraveCat() => Fire(OwnHappening.MogGraveCat);

        [DebugAction("Rimfolk", "Gods: Tamiu's riddles at supper now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MogRiddle() => Fire(OwnHappening.MogRiddle);

        [DebugAction("Rimfolk", "Gods: Murmur's splash at the water now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MurmurSplash() => Fire(OwnHappening.MurmurSplash);

        [DebugAction("Rimfolk", "Gods: Murmur's frog in the bedroom now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MurmurFrogIn() => Fire(OwnHappening.MurmurFrogIn);

        [DebugAction("Rimfolk", "Gods: Ducat's coin passed hand to hand now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DucatPassed() => Fire(OwnHappening.DucatPassed);

        [DebugAction("Rimfolk", "Gods: Ducat's miser's silver scattered now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DucatMiser() => Fire(OwnHappening.DucatMiser);

        [DebugAction("Rimfolk", "Gods: Revel's one cup between two at odds now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RevelRivalsCup() => Fire(OwnHappening.RevelRivalsCup);

        [DebugAction("Rimfolk", "Gods: Revel's wager lost now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RevelWager() => Fire(OwnHappening.RevelWager);

        // ---- The gods' rarest gifts (Gods/Touch/GodRareGifts.cs) ----

        [DebugAction("Rimfolk", "Gods: a rare gift (a god's weapon) now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RareWeapon()
        {
            var list = GodRareGiftsMath.Weapons.Keys.Select(k => new DebugMenuOption(k == "Mog" ? "Tamiu" : k, DebugMenuOptionMode.Action, () => Say(GodRareGifts.DevWeapon(Find.CurrentMap, k)))).ToList();
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }

        [DebugAction("Rimfolk", "Gods: a rare gift (a drake's egg in the deep) now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RareEgg() => Say(GodRareGifts.DevEgg(Find.CurrentMap));
    }
}
