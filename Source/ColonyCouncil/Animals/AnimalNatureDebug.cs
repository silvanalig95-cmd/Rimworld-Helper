using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the animals' natures (Debug actions, "Rimfolk", "Animals: ..."). Each brings one moment about now past its
    /// pace and its wait, where what it needs is on the map; what follows is the moment's own (Animals/AnimalTempers.cs,
    /// AnimalBonds.cs, AnimalVoices.cs).
    /// </summary>
    public static class AnimalNatureDebug
    {
        private static void Say(string text) => Messages.Message("Animals: " + text, MessageTypeDefOf.SilentInput, false);

        private static List<Pawn> Animals => Find.CurrentMap?.mapPawns.SpawnedColonyAnimals.Where(AnimalTempers.Has).ToList() ?? new List<Pawn>();

        private static void Menu(IEnumerable<FloatMenuOption> options)
        {
            var list = options.ToList();
            if (list.Count == 0) list.Add(new FloatMenuOption("(no named or bonded animal here)", null));
            Find.WindowStack.Add(new FloatMenu(list));
        }

        private static string Label(Pawn a)
        {
            var t = AnimalTempers.Of(a);
            return a.LabelShort + " (" + AnimalFacts.KindLabel(a) + ", " + (t != null ? AnimalNatureMath.Key(t.Value) : "none") + ")";
        }

        [DebugAction("Rimfolk", "Animals: each named or bonded animal's temperament", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugList()
        {
            var lines = Animals.Select(a => "  " + Label(a) + ", family " + AnimalTempers.Facts(a).family + (AnimalTempers.Lately(a) is string l ? ", lately: " + l : "")).ToList();
            var book = AnimalBook.Instance;
            if (book != null)
                lines.Add("  ties: " + string.Join("; ", book.ties.Select(t => t.nameA + " and " + t.nameB + " " + (t.kind == AnimalTie.Friends ? "friends" : t.kind == AnimalTie.Rivals ? "rivals" : "pair, " + t.nights + " nights")
                                                                         + (t.Lost ? " (one lost)" : ""))) + "; gone with their handler: " + book.gone.Count);
            Log.Message("[Rimfolk] Animals' temperaments here:\n" + string.Join("\n", lines));
            Say(Animals.Count + " named or bonded animals here; the list is in the log.");
        }

        [DebugAction("Rimfolk", "Animals: a temperament's moment now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMoment()
        {
            Menu(Animals.Select(a => new FloatMenuOption(Label(a), () =>
            {
                var t = AnimalTempers.Of(a);
                if (t == null) return;
                Say(AnimalTempers.TryMoment(a, t.Value, out string why) ? a.LabelShort + " showed it (" + AnimalNatureMath.Key(t.Value) + "): " + (AnimalTempers.Lately(a) ?? "")
                    : a.LabelShort + " couldn't now: " + why);
            })));
        }

        [DebugAction("Rimfolk", "Animals: give an animal a temperament...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSetTemper()
        {
            Menu(Animals.Select(a => new FloatMenuOption(Label(a), () =>
                Menu(AnimalNatureMath.All.Select(t => new FloatMenuOption(AnimalNatureMath.Key(t), () =>
                {
                    AnimalBook.Instance?.SetTemper(a.thingIDNumber, t);
                    Say(a.LabelShort + " is " + AnimalNatureMath.Key(t) + " now.");
                }))))));
        }

        [DebugAction("Rimfolk", "Animals: the fight's look now (the brave stand, the timid bolt)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFight() => Say(AnimalTempers.DebugFight(Find.CurrentMap));

        [DebugAction("Rimfolk", "Animals: make two friends or rivals...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTie()
        {
            var book = AnimalBook.Instance;
            if (book == null) return;
            Menu(Animals.Select(x => new FloatMenuOption(Label(x), () =>
                Menu(Animals.Where(y => y != x).SelectMany(y => new[]
                {
                    new FloatMenuOption("friends with " + y.LabelShort, () =>
                    {
                        AnimalBonds.BecomeFriendsNow(book, x, y);
                        Say(x.LabelShort + " and " + y.LabelShort + " are friends.");
                    }),
                    new FloatMenuOption("rivals with " + y.LabelShort, () =>
                    {
                        AnimalBonds.BecomeRivals(book, x, y);
                        Say(x.LabelShort + " and " + y.LabelShort + " are rivals.");
                    })
                })))));
        }

        [DebugAction("Rimfolk", "Animals: a friends' or rivals' moment now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBondMoment() => Say(AnimalBonds.DebugMoment(Find.CurrentMap));

        [DebugAction("Rimfolk", "Animals: a grieving animal goes to the grave now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGrief() => Say(AnimalBonds.DebugGrief(Find.CurrentMap));

        [DebugAction("Rimfolk", "Animals: release an animal, remembering its handler...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRelease()
        {
            Menu(Animals.Select(a => new FloatMenuOption(Label(a), () =>
            {
                var handler = PetTracker.PersonOf(a);
                if (handler == null)
                {
                    Say(a.LabelShort + " has no person to remember.");
                    return;
                }
                a.SetFaction(null);
                var book = AnimalBook.Instance;
                book?.Left(a, handler, true);
                // As if two days had gone by, so the next look (or the dev action) may bring the meeting.
                var g = book?.gone.LastOrDefault();
                if (g != null) g.tick -= (int)(AnimalNatureMath.MeetAfterDays * GenDate.TicksPerDay);
                Say(a.LabelShort + " is wild now, and remembers " + handler.LabelShort + ".");
            })));
        }

        [DebugAction("Rimfolk", "Animals: an old animal meets its handler now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMeeting() => Say(AnimalBonds.DebugMeeting(Find.CurrentMap));

        [DebugAction("Rimfolk", "Animals: who could speak here", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWho() => Say(AnimalVoices.DebugWho(Find.CurrentMap));

        [DebugAction("Rimfolk", "Animals: let an animal speak (testing)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLetSpeak()
        {
            Menu(Animals.Select(a => new FloatMenuOption(Label(a), () =>
            {
                AnimalVoices.LetSpeak(a);
                Say(a.LabelShort + " may speak until the game is loaded again (with the setting on, or a fantasy source on Auto).");
            })));
        }

        [DebugAction("Rimfolk", "Animals: a speaking animal says something now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSay()
        {
            Menu(Animals.Where(a => AnimalVoices.GiftOf(a, out _) != AnimalGift.None).Select(a => new FloatMenuOption(Label(a) + ", " + AnimalVoices.GiftOf(a, out _),
                () => Say(AnimalVoices.DebugSay(a)))));
        }

        [DebugAction("Rimfolk", "Animals: a spirit animal's dream for its person...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDream()
        {
            var comp = CouncilGameComponent.Instance;
            var def = DefDatabase<DreamDef>.GetNamedSilentFail("Council_Dream_SpiritAnimal");
            var options = Find.CurrentMap.mapPawns.FreeColonistsSpawned.Where(p => AnimalVoices.SpiritOf(p) != null)
                .Select(p => new FloatMenuOption(p.LabelShort + " (" + AnimalVoices.SpiritOf(p).LabelShort + ")", () =>
                    Say(comp != null && def != null && comp.dreams.TryDream(comp, p, false, def) ? p.LabelShort + " dreamt of " + AnimalVoices.SpiritOf(p)?.LabelShort + "."
                        : "the dream didn't fit them now."))).ToList();
            if (options.Count == 0) options.Add(new FloatMenuOption("(nobody with a spirit animal: a tribe, or a faith of animal personhood, with a bonded animal and speech on)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Animals: the council's question has a word from a speaking animal", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDilemma()
        {
            AnimalVoices.DebugNextDilemma = true;
            Say("the next council question about someone whose animal speaks (or about the animal) carries its word.");
        }
    }
}
