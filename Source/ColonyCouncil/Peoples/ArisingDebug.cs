using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: peoples that arise during play (Debug actions, "Rimfolk", "Peoples: ..."). A people of each cause made
    /// now whatever the pace and the rolls, from the cause the world has (the last fallen town in reach, the most
    /// prosperous towns, the last one the council sent away, the last war of words), or a stand-in where it has none (a
    /// town in reach as if it fell, a stranger as if sent away, a people with a faith as if it split), so each can be
    /// tried in any save; and what could arise now and why not, in the log.
    /// </summary>
    public static class ArisingDebug
    {
        private static void Say(string text) => Messages.Message("Peoples: " + text, MessageTypeDefOf.SilentInput, false);

        private static int Now => Find.TickManager.TicksGame;

        [DebugAction("Rimfolk", "Peoples: a new people arises now (testing)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugArise()
        {
            var options = new List<FloatMenuOption>();
            foreach (var kind in ArisingMath.Kinds)
            {
                string k = kind;
                options.Add(new FloatMenuOption(k, () =>
                {
                    int now = Now;
                    var c = Arising.FindCause(k, now / GenDate.TicksPerDay, now, true) ?? StandIn(k, Find.CurrentMap);
                    if (c == null)
                    {
                        Say(k + ": no cause, and no stand-in" + (k == ArisingMath.Splinter && !ModsConfig.IdeologyActive ? " (it needs Ideology)" : "") + ".");
                        return;
                    }
                    var f = Arising.Arise(c);
                    var town = f != null ? Find.WorldObjects.Settlements.FirstOrDefault(s => s.Faction == f) : null;
                    Say(f == null ? k + ": couldn't arise (no template for its age, or no tile for its town near " + (c.name ?? "the cause") + ")."
                        : f.Name + " arose (" + k + (c.standIn ? ", from a stand-in cause" : "") + ")" + (town != null ? ", their " + (Peoples.TownWord(f) ?? "town") + " " + town.Name : "") + ".");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>A stand-in for a cause the world doesn't have, so it can be tried: the words name it as if it had happened.</summary>
        private static ArisingCause StandIn(string kind, Map map)
        {
            if (map == null) return null;
            switch (kind)
            {
                case ArisingMath.Refugees:
                    foreach (var reach in RegionTowns.Reach)
                        for (int i = 0; i < reach.towns.Count; i++)
                        {
                            var t = reach.towns[i];
                            if (t?.Faction == null || t.Faction.IsPlayer || Peoples.IsOurs(t.Faction)) continue;
                            return new ArisingCause
                            {
                                kind = kind, key = ArisingMath.Key(kind, -Now - t.ID), age = Ages.Of(t.Faction), near = t.Tile, minTiles = 2, maxTiles = 6, home = reach.home,
                                tiles = reach.tiles[i], parent = t.Faction, name = t.Name, since = Now, standIn = true
                            };
                        }
                    return null;
                case ArisingMath.Exiles:
                    var home = Homes.Seat ?? map;
                    var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                        developmentalStages: DevelopmentalStage.Adult));
                    if (p == null) return null;
                    Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                    return new ArisingCause
                    {
                        kind = kind, key = ArisingMath.Key(kind, -Now - p.thingIDNumber), age = Ages.Ours, near = home.Tile, minTiles = RiseFall.GoodwillTiles + 2,
                        maxTiles = RiseFall.GoodwillTiles + 9, home = home, tiles = 0f, exile = p, name = p.LabelShort, since = Now, standIn = true
                    };
                default:
                    return null;
            }
        }

        [DebugAction("Rimfolk", "Peoples: what could arise now", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugWhat()
        {
            var book = ArisingBook.Instance;
            if (book == null)
            {
                Say("no book.");
                return;
            }
            int now = Now, day = now / GenDate.TicksPerDay;
            var sb = new StringBuilder("[Rimfolk] New peoples: setting " + Arising.Setting.ToStringPercent() + (Arising.On ? "" : " (off)") + ", day " + day
                                       + ", standing " + book.Standing + "/" + ArisingMath.MaxStanding + ", last arose " + Days(book.LastAt(), now) + ".");
            sb.AppendLine();
            foreach (var kind in ArisingMath.Kinds)
            {
                bool pace = ArisingMath.PaceAllows(Arising.Setting, now, book.LastAt(), book.LastAt(kind), book.Standing);
                var strict = Arising.FindCause(kind, day, now, false);
                var loose = strict ?? Arising.FindCause(kind, day, now, true);
                sb.AppendLine("  " + kind + ": the pace " + (pace ? "allows it" : "holds it back") + " (last of it " + Days(book.LastAt(kind), now) + "); "
                              + (strict != null ? "due today: " + Describe(strict) : loose != null ? "a cause, not due today: " + Describe(loose) : "no cause")
                              + (loose != null ? ", template " + (Arising.TemplateFor(loose)?.defName ?? "none") : "") + ".");
            }
            foreach (var r in book.arisen)
                sb.AppendLine("  arose: " + (r.people?.Name ?? "(gone)") + " (" + r.kind + ", " + r.cause + ", from " + r.from + ", " + Days(r.tick, now) + ")"
                              + (r.people != null ? ", goodwill " + r.people.PlayerGoodwill : "") + ".");
            Log.Message(sb.ToString().TrimEnd());
            Say("listed in the log.");
        }

        private static string Days(int tick, int now) => tick < 0 ? "never" : ((now - tick) / (float)GenDate.TicksPerDay).ToString("0.#") + " days ago";

        private static string Describe(ArisingCause c) =>
            (c.name ?? ArisingWords.Towns(c.towns)) + (c.parent != null ? " of " + c.parent.Name : "") + ", " + c.age + (c.byColony ? ", taken by the colony" : "")
            + (c.exile != null ? ", stood by " + c.stoodBy + ", glad " + c.glad + (c.record?.grudge == true ? ", a grudge" : "") : "") + (c.debate ? ", a debate" : "");
    }
}
