using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the gods ("Rimfolk", "Gods: ..."). The pantheon in the log, with its ties and the colony's favour; favour
    /// added to a god at this home; how real the gods are, set; every god made known; a deed of a domain done; a tie
    /// shifted; a newborn of a domain; the pantheon built again for this world; the Gods page opened.
    /// </summary>
    public static class GodsDebug
    {
        private static void Say(string text) => Messages.Message("Gods: " + text, MessageTypeDefOf.SilentInput, false);

        private static bool Ready()
        {
            if (!Gods.On) Say("the gods are off (the setting \"Gods\").");
            else if (GodBook.Instance == null) Say("no pantheon in this game.");
            else return true;
            return false;
        }

        [DebugAction("Rimfolk", "Gods: show the pantheon (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugShow()
        {
            if (!Ready()) return;
            var b = GodBook.Instance;
            var map = Find.CurrentMap;
            var sb = new StringBuilder("[Rimfolk] Gods: " + b.gods.Count + " gods, " + b.ties.Count + " ties, realness " + Gods.Realness + ", patron here "
                                       + (Gods.Patron(map)?.name ?? "none") + ".\n");
            foreach (var g in b.gods)
            {
                sb.Append("  ").Append(g.id).Append(' ').Append(Gods.FullName(g)).Append(" (").Append(Gods.GodOf(g)).Append("; ").Append(g.domain);
                if (g.second != GodDomain.None) sb.Append('+').Append(g.second);
                sb.Append(", ").Append(g.form).Append(", ").Append(g.kind).Append(", ").Append(g.nature).Append(", ").Append(g.source);
                if (g.patron != null) sb.Append(", patron");
                sb.Append(") held by ").Append(Gods.PeopleWords(g)).Append("; ").Append(g.known ? "known (" + g.knownHow + ")" : "rumour")
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
            foreach (var g in Gods.All.OrderBy(x => x.name))
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
            Say("every god known.");
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
                    Say("a deed of " + domain + " done; deeds this year here: " + GodBook.Instance.DeedsThisYear(domain, Gods.HomeKey(map)) + ".");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Gods: shift a tie now", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugShift()
        {
            if (!Ready()) return;
            var b = GodBook.Instance;
            var t = b.ties.RandomElementWithFallback();
            if (t == null)
            {
                Say("no ties.");
                return;
            }
            GodTiesMath.Shift(t.kind, t.strength, Rand.Value, Rand.Value, out var next, out float strength, out bool gone);
            string was = t.kind.ToString();
            if (gone) b.ties.Remove(t);
            else
            {
                t.kind = next;
                t.strength = strength;
                t.since = Find.TickManager.TicksGame;
            }
            Say(b.ById(t.a)?.name + " and " + b.ById(t.b)?.name + ": " + was + " -> " + (gone ? "nothing" : next + " (" + strength.ToString("0.00") + ")") + ".");
        }

        [DebugAction("Rimfolk", "Gods: a newborn of the colony's faith...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNewborn()
        {
            if (!Ready()) return;
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var d in GodsMath.Domains)
            {
                var domain = d;
                options.Add(new FloatMenuOption(d.ToString(), () =>
                {
                    var faith = ModsConfig.IdeologyActive ? Faction.OfPlayer?.ideos?.PrimaryIdeo : null;
                    var g = Pantheon.Born(GodBook.Instance, map, domain, faith, Find.TickManager.TicksGame);
                    Say(g != null ? g.name + " is born." : "none born.");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Gods: build the pantheon again (testing)", allowedGameStates = AllowedGameStates.Playing)]
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
            b.newborns = 0;
            b.lastNewborn = -1;
            b.built = false;
            GodsPage.chosen = GodsPage.NoneChosen;
            Pantheon.Build(b);
            Say("built again: " + b.gods.Count + " gods, " + b.ties.Count + " ties.");
        }

        [DebugAction("Rimfolk", "Gods: open the Gods page", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugPage() => Pages.Open(RegionView.Peoples + "/Gods");
    }
}
