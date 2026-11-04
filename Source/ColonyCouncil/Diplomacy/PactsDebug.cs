using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force each part of pacts and truces at the home on screen (Debug actions, "Rimfolk"): their offer of a
    /// truce, a truce agreed with their envoy due now, the envoy sent at once, their firebrands now, a truce's end; a pact
    /// made of either kind, their offer of one, their band due now, their call to arms for one of their towns (against a
    /// people at war with them), a trade pact's season now. Where it goes from there plays by the rules.
    /// </summary>
    public static class PactsDebug
    {
        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static float Day => Find.TickManager.TicksGame / (float)PactMath.DayTicks;

        private static void Say(string text) => Messages.Message("Pacts and truces: " + text, MessageTypeDefOf.SilentInput, false);

        private static IEnumerable<Faction> Peoples(bool hostile) => Find.FactionManager.AllFactionsListForReading.Where(f => Diplomacy.Deals(f) && Diplomacy.Hostile(f) == hostile);

        private static void Menu(List<FloatMenuOption> options, string none)
        {
            if (options.Count == 0) options.Add(new FloatMenuOption(none, null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Truces: their offer now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTheirOffer()
        {
            var home = Home;
            Menu(Peoples(true).Select(f => new FloatMenuOption(f.Name, () => Truces.Offer(f, home, PactMath.TheirPrice(Rand.Value)))).ToList(), "(no people at war with the colony)");
        }

        [DebugAction("Rimfolk", "Truces: one agreed, their envoy due now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAgreed()
        {
            var home = Home;
            var options = new List<FloatMenuOption>();
            foreach (var f in Peoples(true))
                foreach (TrucePrice price in Enum.GetValues(typeof(TrucePrice)))
                {
                    var people = f;
                    var p = price;
                    options.Add(new FloatMenuOption(f.Name + ": " + PactWords.Price(p, 200), () =>
                    {
                        var t = Truces.MakePending(people, home, TruceTerm.Season, p, p == TrucePrice.Ours || p == TrucePrice.Theirs ? 200 : Truces.Captives(people, home).Count, true, null, 0.01f);
                        Say(t != null ? "agreed; their envoy comes within the hour if the land is quiet." : "a truce with them is live already.");
                    }));
                }
            Menu(options, "(no people at war with the colony)");
        }

        [DebugAction("Rimfolk", "Truces: their firebrands now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFirebrands()
        {
            Menu(Truces.Live().Where(Truces.Sworn).Select(t => new FloatMenuOption(Truces.PeopleOf(t)?.Name ?? "?", () =>
            {
                t.terms.days = Day;
                WorldLedger.Touch(t);
                Say("their firebrands come within the hour.");
            })).ToList(), "(no sworn truce)");
        }

        [DebugAction("Rimfolk", "Truces: end one now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEnd()
        {
            Menu(Truces.Live().Where(Truces.Sworn).Select(t => new FloatMenuOption(Truces.PeopleOf(t)?.Name ?? "?", () =>
            {
                t.terms.number = Day;
                WorldLedger.Touch(t);
                Say("it reaches its end within the hour: a peace if their goodwill has come to 0.");
            })).ToList(), "(no sworn truce)");
        }

        [DebugAction("Rimfolk", "Pacts: make one...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMake()
        {
            var home = Home;
            var options = new List<FloatMenuOption>();
            foreach (var f in Peoples(false))
                foreach (PactKind k in Enum.GetValues(typeof(PactKind)))
                {
                    var people = f;
                    var kind = k;
                    options.Add(new FloatMenuOption(f.Name + ": " + PactWords.Kind(k), () =>
                    {
                        var goods = Pacts.CartGoods(home);
                        var (def, count) = goods.Count > 0 ? goods[0] : (null, 0);
                        var t = Pacts.Make(people, home, kind, true, def, count);
                        Say(t != null ? "made." : "one of that kind is live already.");
                    }));
                }
            Menu(options, "(no people at peace)");
        }

        [DebugAction("Rimfolk", "Pacts: their offer now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOffer()
        {
            var home = Home;
            Menu(Peoples(false).Select(f => new FloatMenuOption(f.Name, () => Pacts.Offer(f, home, PactKind.Defence, Pacts.CommonEnemy(f), null))).ToList(), "(no people at peace)");
        }

        [DebugAction("Rimfolk", "Pacts: their band due now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBand()
        {
            Menu(Pacts.Live().Where(t => Pacts.KindOf(t) == PactKind.Defence && !Pacts.CallUnderWay(t)).Select(t => new FloatMenuOption(Pacts.PeopleOf(t)?.Name ?? "?", () =>
            {
                t.terms.goods = PactMath.BandCode(Day);
                t.terms.days = Day;
                WorldLedger.Touch(t);
                Say("their band walks in within the hour (only while a threat is on the map; else they turn back).");
            })).ToList(), "(no defence pact without a call under way)");
        }

        [DebugAction("Rimfolk", "Pacts: their call to arms now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugArms()
        {
            var home = Home;
            var options = new List<FloatMenuOption>();
            foreach (var t in Pacts.Live().Where(x => Pacts.KindOf(x) == PactKind.Defence && !Pacts.CallUnderWay(x)))
            {
                var f = Pacts.PeopleOf(t);
                var town = WorldSituationWorker.NearestTown(f, home);
                var enemy = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(o => o != f && Raids.CanRaid(o) && o.HostileTo(f));
                if (town == null || enemy == null) continue;
                var tie = t;
                options.Add(new FloatMenuOption(f.Name + ": " + town.Name + " against " + enemy.Name, () =>
                {
                    tie.terms.goods = PactMath.ArmsCode(town.ID, Day + PactMath.SetOutDays);
                    tie.terms.about = Refs.Of(enemy);
                    WorldLedger.Touch(tie);
                    CallsToArms.Letter(tie, f, home, town, enemy, true);
                }));
            }
            Menu(options, "(no defence pact with a town near and an enemy of theirs)");
        }

        [DebugAction("Rimfolk", "Pacts: a trade season now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSeason()
        {
            Menu(Pacts.Live().Where(t => Pacts.KindOf(t) == PactKind.Trade).Select(t => new FloatMenuOption(Pacts.PeopleOf(t)?.Name ?? "?", () =>
            {
                t.terms.days = 0f;
                WorldLedger.Touch(t);
                Say("their caravan and the cart come within the hour, by day.");
            })).ToList(), "(no trade pact)");
        }
    }
}
