using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the gods ("Rimfolk", "Gods: ..."), the only place their hidden state shows. The gods in the log, with their
    /// ties, who reveres them and the colony's favour; favour added to a god at this home; how real the gods are, set; every
    /// god made known; a deed of a domain done; a god's hand shown here; every story heard; the colony's book opened, and a
    /// name in it worked out; the gods made again for this world.
    /// </summary>
    public static class GodsDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        private static bool Ready()
        {
            if (!Gods.On) Say("the gods are off (the setting \"Gods\").");
            else if (GodBook.Instance == null) Say("no gods in this game.");
            else return true;
            return false;
        }

        [DebugAction("Rimfolk", "Gods: show the gods (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugShow()
        {
            if (!Ready()) return;
            var b = GodBook.Instance;
            var map = Find.CurrentMap;
            var lore = GodLore.Instance;
            var sb = new StringBuilder("[Rimfolk] Gods: " + b.gods.Count + " gods, " + b.ties.Count + " ties, realness " + Gods.Realness + ", patron here "
                                       + (Gods.Patron(map)?.name ?? "none") + "; the book: " + (lore?.entries.Count ?? 0) + " entries, " + (lore?.names.Count ?? 0) + " names, keeper "
                                       + (GodLore.Keeper(out var voice)?.LabelShort ?? "none") + " (" + voice + ").\n");
            foreach (var g in b.gods)
            {
                sb.Append("  ").Append(g.id).Append(' ').Append(Gods.FullName(g)).Append(" (").Append(g.domain);
                if (g.second != GodDomain.None) sb.Append('+').Append(g.second);
                sb.Append(", ").Append(g.kind).Append(", ").Append(g.nature).Append(") revered by ").Append(Gods.PeopleWords(g)).Append("; ")
                    .Append(g.known ? "known (" + g.knownHow + ")" : "not known").Append("; the book calls it ").Append(lore?.PageOf(g)?.name ?? "nothing yet")
                    .Append("; favour here ").Append(Gods.Favour(g, map)).Append(" (").Append(Gods.FavourValue(g, map).ToString("0.0")).Append(")\n");
                foreach (var t in Gods.TiesOf(g).Where(t => t.a == g.id))
                    sb.Append("      ").Append(t.kind).Append(" with ").Append(Gods.ById(t.b)?.name ?? "?").Append(" (").Append(t.strength.ToString("0.00")).Append(")\n");
            }
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Gods: add favour here...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFavour()
        {
            if (!Ready()) return;
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var g in Gods.All)
            {
                var god = g;
                options.Add(new FloatMenuOption(Gods.FullName(g) + " (" + Gods.Favour(g, map) + ")", () =>
                {
                    var amounts = new List<FloatMenuOption>();
                    foreach (float a in new[] { 25f, 10f, -10f, -25f })
                    {
                        float by = a;
                        amounts.Add(new FloatMenuOption((by > 0 ? "+" : "") + by, () =>
                        {
                            Gods.AddFavour(god, map, by, "CC_God_Dev".Translate(), FavourSource.Dev);
                            Say(god.name + " is " + Gods.Favour(god, map) + " here now.");
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(amounts));
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Gods: set how real the gods are...", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugRealness()
        {
            var s = ColonyCouncilMod.Settings;
            if (s == null) return;
            var options = new List<FloatMenuOption>();
            foreach (GodRealness r in System.Enum.GetValues(typeof(GodRealness)))
            {
                var real = r;
                options.Add(new FloatMenuOption(r + (s.godRealness == r ? " (now)" : ""), () =>
                {
                    s.godRealness = real;
                    Say("how real the gods are: " + real + ".");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Gods: the colony knows every god", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugLearn()
        {
            if (!Ready()) return;
            foreach (var g in Gods.All) Gods.Learn(g, "neighbours", false);
            Gods.Changed();
            Say("every god known (hidden state; the book only has what its people heard).");
        }

        [DebugAction("Rimfolk", "Gods: a deed of a domain here...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDeed()
        {
            if (!Ready()) return;
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var d in GodsMath.Domains)
            {
                var domain = d;
                options.Add(new FloatMenuOption(d.ToString(), () =>
                {
                    Gods.Deed(domain, map, 5f, "CC_God_Dev".Translate());
                    Say("a deed of " + domain + " done.");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Gods: an event's hand here...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHand()
        {
            if (!Ready()) return;
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var g in Gods.All)
                foreach (HandKind k in System.Enum.GetValues(typeof(HandKind)))
                {
                    var god = g;
                    var kind = k;
                    options.Add(new FloatMenuOption(g.name + ": " + k, () =>
                    {
                        bool shown = GodHands.Show(map, god, kind, "a test of the gods' hands (dev)", Find.TickManager.TicksGame);
                        Say(shown ? "remarked on; see the book." : "nobody here would say it (no believer awake).");
                    }));
                }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Gods: hear every story (testing)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStories()
        {
            if (!Ready()) return;
            var teller = Find.CurrentMap?.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (teller == null)
            {
                Say("nobody here to tell them.");
                return;
            }
            int told = 0;
            for (int i = 0; i < 300 && GodTales.Tell(teller, null, null, "Supper"); i++) told++;
            Say(told + " pieces told by " + teller.LabelShort + "; see the book.");
        }

        [DebugAction("Rimfolk", "Gods: work out a name in the book", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugConnect()
        {
            var lore = GodLore.Instance;
            if (!Ready() || lore == null) return;
            var group = lore.names.Where(n => n.god >= 0 && n.joinedTo == null).GroupBy(n => n.god).FirstOrDefault(gr => gr.Count() >= 2);
            if (group == null)
            {
                Say("no god in the book under two names yet.");
                return;
            }
            var pages = group.OrderBy(n => n.first).ToList();
            var by = Find.CurrentMap?.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            lore.Connect(pages[1], pages[0], by, by == null ? "a traveler" : null);
            Say(pages[1].name + " worked out to be " + pages[0].name + ".");
        }

        [DebugAction("Rimfolk", "Gods: open the book of the gods", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugPage() => Pages.Open(RegionView.Gods);

        [DebugAction("Rimfolk", "Gods: make the gods again (testing)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugRebuild()
        {
            if (!Ready()) return;
            var b = GodBook.Instance;
            b.gods.Clear();
            b.ties.Clear();
            b.standings.Clear();
            b.acts.Clear();
            b.vows.Clear();
            b.deeds.Clear();
            b.nextId = 1;
            b.built = false;
            b.version = 0;
            Pantheon.Build(b);
            b.retired = false;
            Say("made again: " + b.gods.Count + " gods, " + b.ties.Count + " ties.");
        }
    }
}
