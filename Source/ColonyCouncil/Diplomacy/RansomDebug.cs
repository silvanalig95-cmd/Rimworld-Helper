using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force each part of ransom and the envoy at the home on screen (Debug actions, "Rimfolk"): their offer for a
    /// prisoner now, the game's demand by messenger now, an exchange agreed and their party due now, the next exchange's
    /// trap, a waiting offer due now; their envoy's offer, their envoy arriving now, a letter home due, the stay's end now;
    /// the colony's envoy at their town now, a letter from them, their season's end. Where it goes from there plays by the
    /// rules.
    /// </summary>
    public static class RansomDebug
    {
        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static int Now => Find.TickManager.TicksGame;

        private static void Say(string text) => Messages.Message("Ransom and the envoy: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Menu(List<FloatMenuOption> options, string none)
        {
            if (options.Count == 0) options.Add(new FloatMenuOption(none, null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Ransom: their offer for a prisoner now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOffer()
        {
            var home = Home;
            var book = DiplomacyBook.Instance;
            if (book == null) return;
            Menu(home.mapPawns.PrisonersOfColonySpawned.Where(p => Ransoms.Deals(p.Faction)).Select(p => new FloatMenuOption(p.LabelShort + " (" + p.Faction.Name + ")", () =>
            {
                var h = book.HomeOf(home);
                var o = Ransoms.For(p) ?? h.offers.FirstOrDefault(x => x.captive == p);
                if (o == null)
                {
                    Ransoms.HearOffer(p, home, 0.001f, out bool yes);
                    if (!yes)
                    {
                        // Not wanted by the roll: made anyway for the test.
                        h.offers.Add(new RansomOffer
                        {
                            id = book.NextId(), kind = RansomKind.Theirs, captive = p, people = p.Faction, due = Now, created = Now, captiveName = p.LabelShort,
                            price = RansomMath.Price(p.MarketValue, false, false, Rand.Value)
                        });
                    }
                    o = h.offers.FirstOrDefault(x => x.captive == p);
                }
                if (o != null)
                {
                    o.stage = RansomStage.Waiting;
                    o.due = Now;
                }
                Say("their word comes within the hour if the land is quiet (a messenger, a go-between, or a call).");
            })).ToList(), "(no prisoner of a people the colony deals with)");
        }

        [DebugAction("Rimfolk", "Ransom: the game's demand by messenger now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDemand()
        {
            Say(Ransoms.Demand(Home) ? "their messenger walks in to the gate." : "nobody of the colony's held by a people who can send word.");
        }

        [DebugAction("Rimfolk", "Ransom: an exchange agreed, their party due now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugExchange()
        {
            var home = Home;
            var options = new List<FloatMenuOption>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(Ransoms.Deals))
                foreach (var (ours, theirs) in Ransoms.Pairs(f, home))
                {
                    var o = ours;
                    var t = theirs;
                    options.Add(new FloatMenuOption(o.LabelShort + " for " + t.LabelShort, () =>
                    {
                        string why = Ransoms.ExchangeWhyNot(o, t, home);
                        if (why != null)
                        {
                            Say(why);
                            return;
                        }
                        var book = DiplomacyBook.Instance;
                        book.HomeOf(home).offers.Add(new RansomOffer
                        {
                            id = book.NextId(), kind = RansomKind.Exchange, captive = t, ours = o, people = f, due = Now, created = Now, captiveName = t.LabelShort, oursName = o.LabelShort
                        });
                        Say("their party walks in within the hour with " + o.LabelShort + ".");
                    }));
                }
            Menu(options, "(no colonist held by a people whose own the colony holds)");
        }

        [DebugAction("Rimfolk", "Ransom: the next exchange with a hostile people is a trap", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTrap()
        {
            Ransoms.forceTrap = true;
            Say("the next exchange at the gate with a hostile people springs the trap.");
        }

        [DebugAction("Rimfolk", "Envoy: their offer now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEnvoyOffer()
        {
            var home = Home;
            var book = DiplomacyBook.Instance;
            Menu(Find.FactionManager.AllFactionsListForReading.Where(f => Diplomacy.Deals(f) && !Diplomacy.Hostile(f) && Envoys.EnvoyOf(f, home) != null)
                .Select(f => new FloatMenuOption(f.Name, () => Envoys.Offer(book.HomeOf(home), home, f))).ToList(), "(no people at peace whose envoy the colony knows)");
        }

        [DebugAction("Rimfolk", "Envoy: theirs arrives now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEnvoyArrive()
        {
            var home = Home;
            Menu(Find.FactionManager.AllFactionsListForReading.Where(f => Diplomacy.Deals(f) && !Diplomacy.Hostile(f) && Envoys.EnvoyOf(f, home) != null)
                .Select(f => new FloatMenuOption(f.Name, () =>
                {
                    var s = Envoys.Agree(home, f, null, true, 0.001f);
                    Say(s != null ? "their envoy walks in within the hour if the land is quiet." : "an envoy is under way at this home already.");
                })).ToList(), "(no people at peace whose envoy the colony knows)");
        }

        [DebugAction("Rimfolk", "Envoy: theirs, a letter home due now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEnvoyLetter()
        {
            var s = Envoys.At(Home);
            if (s == null || s.stage != EnvoyStage.Staying)
            {
                Say("no envoy staying at this home.");
                return;
            }
            s.since = Mathf.Min(s.since, Now - Mathf.RoundToInt(EnvoyMath.LetterDays * (s.letters + 1) * EnvoyMath.DayTicks));
            s.until = Mathf.Max(s.until, Now + 2 * EnvoyMath.DayTicks);
            Say("their runner comes to the gate within the hour.");
        }

        [DebugAction("Rimfolk", "Envoy: theirs, the season's end now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEnvoyEnd()
        {
            var s = Envoys.At(Home);
            if (s == null)
            {
                Say("no envoy at this home.");
                return;
            }
            s.until = Now;
            Say("their stay ends within the hour (" + RansomWords.Mood(Envoys.Mood(s)) + ").");
        }

        [DebugAction("Rimfolk", "Envoy: ours at their town now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAbroadThere()
        {
            foreach (var (_, a) in EnvoysAbroad.All())
            {
                var c = a.colonist?.GetCaravan();
                var town = EnvoysAbroad.TownOf(a);
                if (c != null && town != null && a.stage == EnvoyStage.Staying)
                {
                    c.pather?.StopDead();
                    c.Tile = town.Tile;
                    Say(a.colonist.LabelShort + " is at " + town.Name + "; the hour takes it from there.");
                    return;
                }
            }
            Say("no envoy of the colony's on the road.");
        }

        [DebugAction("Rimfolk", "Envoy: ours, a letter now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAbroadLetter()
        {
            foreach (var (_, a) in EnvoysAbroad.All())
                if (a.stage == EnvoyStage.There)
                {
                    a.since = Mathf.Min(a.since, Now - Mathf.RoundToInt(EnvoyMath.LetterDays * (a.letters + 1) * EnvoyMath.DayTicks));
                    a.until = Mathf.Max(a.until, Now + 2 * EnvoyMath.DayTicks);
                    Say("their letter comes within the hour.");
                    return;
                }
            Say("no envoy of the colony's at a town.");
        }

        [DebugAction("Rimfolk", "Envoy: ours, the season's end now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAbroadEnd()
        {
            foreach (var (_, a) in EnvoysAbroad.All())
                if (a.stage == EnvoyStage.There)
                {
                    EnvoysAbroad.Recall(a);
                    Say("they set out for home.");
                    return;
                }
            Say("no envoy of the colony's at a town.");
        }
    }
}
