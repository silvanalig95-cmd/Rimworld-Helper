using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force each part of favours owed and called in at the home on screen (Debug actions, "Rimfolk"): a debt of
    /// honour made either way with a people of your choosing; one of theirs called in now, by the ask you pick, with their
    /// answer at once (as their letter would bring it, honoured or refused by the roll); and a people's call on a debt the
    /// colony owes, for the need you pick, whether or not the world shows it. Where it goes from there plays by the rules.
    /// </summary>
    public static class DiplomacyDebug
    {
        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static void Say(string text) => Messages.Message("Favours: " + text, MessageTypeDefOf.SilentInput, false);

        private static IEnumerable<Faction> Peoples() => Find.FactionManager.AllFactionsListForReading.Where(Diplomacy.Deals);

        [DebugAction("Rimfolk", "Favours: make a debt of honour...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMake()
        {
            var home = Home;
            var options = new List<FloatMenuOption>();
            foreach (var f in Peoples())
            {
                var people = f;
                options.Add(new FloatMenuOption(people.Name + ": they owe the colony", () =>
                {
                    var t = FavoursOwed.Make(people, home, true, "foundChild");
                    Say(t != null ? DipWords.DebtLine(t) : "one is open or called in already, or no room.");
                }));
                options.Add(new FloatMenuOption(people.Name + ": the colony owes them", () =>
                {
                    var t = FavoursOwed.Make(people, home, false, "relief");
                    Say(t != null ? DipWords.DebtLine(t) : "one is open or called in already, or no room.");
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no people to deal with)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Favours: call one of theirs in now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCall()
        {
            var home = Home;
            var options = new List<FloatMenuOption>();
            foreach (var t in FavoursOwed.Live().Where(x => !FavoursOwed.ColonyOwes(x)))
            {
                string why = FavourCalls.WhyNot(t);
                if (why != null)
                {
                    options.Add(new FloatMenuOption(DipWords.DebtLine(t) + " (" + why + ")", null));
                    continue;
                }
                foreach (var o in FavourCalls.Options(t, home))
                {
                    var tie = t;
                    string code = o.Code;
                    options.Add(o.why != null
                        ? new FloatMenuOption(DipWords.NameOf(t.from) + ": " + o.label + " (" + o.why + ")", null)
                        : new FloatMenuOption(DipWords.NameOf(t.from) + ": " + o.label, () =>
                        {
                            var heard = FavourCalls.Hear(tie, code, home, null, false, Rand.Value);
                            Tell.Letter(NewsKind.World, Importance.Notable, home, DipWords.People(FavoursOwed.PeopleOf(tie)), heard.line,
                                heard.honoured ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent);
                        }));
                }
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no people owes the colony: make a debt first)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Favours: they call in the colony's debt...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTheyCall()
        {
            var home = Home;
            var options = new List<FloatMenuOption>();
            foreach (var t in FavoursOwed.Live().Where(x => FavoursOwed.ColonyOwes(x) && x.state == Tie.State.Open))
            {
                var f = Refs.Faction(t.to);
                if (f == null) continue;
                foreach (FavourNeed need in System.Enum.GetValues(typeof(FavourNeed)))
                {
                    var tie = t;
                    var n = need;
                    options.Add(new FloatMenuOption(f.Name + ": " + DipWords.Need(n), () =>
                    {
                        if (!FavourAsks.Call(tie, f, n, home)) Say("it couldn't be asked now (a mission that can't happen here, or no letter).");
                    }));
                }
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(the colony owes nobody: make a debt first)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
