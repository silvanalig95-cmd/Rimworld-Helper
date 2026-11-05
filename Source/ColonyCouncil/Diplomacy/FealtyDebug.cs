using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force each part of fealty and vassals at the home on screen (Debug actions, "Rimfolk"): their offer of
    /// protection, an oath agreed with their lord due now, sworn at once (no retinue), the season's tribute due now, what is
    /// owed made due (the oath breaks at the next day), their call on the colony's levies, their band's protection looked
    /// for now, renouncing asked; a struggling town's ask for protection, a town taken under protection at once, a vassal's
    /// ask of each kind, its head's release asked, its harvest now, its levies due now. Where it goes from there plays by
    /// the rules.
    /// </summary>
    public static class FealtyDebug
    {
        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static float Day => Find.TickManager.TicksGame / (float)FealtyMath.DayTicks;

        private static void Say(string text) => Messages.Message("Fealty and vassals: " + text, MessageTypeDefOf.SilentInput, false);

        private static IEnumerable<Faction> AtPeace() => Find.FactionManager.AllFactionsListForReading.Where(f => Diplomacy.Deals(f) && !Diplomacy.Hostile(f) && !Ages.IsEmpire(f));

        private static void Menu(List<FloatMenuOption> options, string none)
        {
            if (options.Count == 0) options.Add(new FloatMenuOption(none, null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Fealty: their offer of protection now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOffer()
        {
            var home = Home;
            Menu(AtPeace().Select(f => new FloatMenuOption(f.Name, () => Fealty.Offer(f, home))).ToList(), "(no people at peace)");
        }

        [DebugAction("Rimfolk", "Fealty: an oath agreed, their lord due now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAgreed()
        {
            var home = Home;
            Menu(AtPeace().Select(f => new FloatMenuOption(f.Name, () =>
            {
                if (Fealty.Of(home) != null)
                {
                    Say("the colony is sworn already.");
                    return;
                }
                Fealty.Agreed(f, home, 0.01f, false);
                Say("agreed; their lord comes within the hour by day if the land is quiet.");
            })).ToList(), "(no people at peace)");
        }

        [DebugAction("Rimfolk", "Fealty: sworn at once...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSworn()
        {
            var home = Home;
            Menu(AtPeace().Select(f => new FloatMenuOption(f.Name, () =>
            {
                var t = Fealty.Swear(f, home, TruceParley.Speaker(home), null);
                Say(t != null ? "sworn." : "the colony is sworn already.");
            })).ToList(), "(no people at peace)");
        }

        [DebugAction("Rimfolk", "Fealty: the season's tribute due now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTribute()
        {
            var t = Fealty.Of(Home);
            if (t == null)
            {
                Say("the colony is sworn to nobody.");
                return;
            }
            t.terms.number = Day;
            WorldLedger.Touch(t);
            Say("due now: their collector comes in the next hour of the day (9 to 16) with no threat on the map.");
        }

        [DebugAction("Rimfolk", "Fealty: tribute owed, its day now (the oath breaks)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOwedDue()
        {
            var t = Fealty.Of(Home);
            if (t == null)
            {
                Say("the colony is sworn to nobody.");
                return;
            }
            if (!Fealty.Owed(t))
            {
                t.terms.count = 100;
                WorldLedger.Set(t, Tie.State.CalledIn);
            }
            t.terms.number = Day;
            WorldLedger.Touch(t);
            Say("owed and due: unpaid by the next hour of the day, the oath breaks.");
        }

        [DebugAction("Rimfolk", "Fealty: their call on the levies now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLevies()
        {
            var home = Home;
            var t = Fealty.Of(home);
            var f = Fealty.LiegeOf(t);
            var reach = RegionTowns.Of(home);
            var town = reach?.towns.FirstOrDefault(s => s != null && s.Faction == f && !s.Destroyed);
            var enemy = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(o => o != null && o != f && !o.IsPlayer && !o.defeated && f != null && o.HostileTo(f) && CallsToArms.Fields(o));
            if (t == null || town == null || enemy == null)
            {
                Say("needs an oath, a town of theirs in reach and a people at war with them.");
                return;
            }
            t.terms.goods = PactMath.ArmsCode(town.ID, Day + PactMath.SetOutDays);
            t.terms.about = Refs.Of(enemy);
            WorldLedger.Touch(t);
            CallsToArms.Letter(t, f, home, town, enemy, true);
            Say("their letter calls the colony's levies to " + town.Name + ".");
        }

        [DebugAction("Rimfolk", "Fealty: their protection looked for now (a raid on the map)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugProtect()
        {
            var t = Fealty.Of(Home);
            if (t == null)
            {
                Say("the colony is sworn to nobody.");
                return;
            }
            t.terms.days = 0f;
            WorldLedger.Touch(t);
            Say("their band is looked for in the next hour, with a raid of a people on the map.");
        }

        [DebugAction("Rimfolk", "Fealty: renouncing asked of the council", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRenounce()
        {
            var t = Fealty.Of(Home);
            Say(t != null && FealtyObjection.AskRenounce(t) ? "the council is asked." : "nothing to renounce, or another question is open.");
        }

        [DebugAction("Rimfolk", "Vassals: a town asks for protection now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugProtection()
        {
            var home = Home;
            var reach = RegionTowns.Of(home);
            var towns = reach?.towns.Where(s => s?.Faction != null && !s.Faction.IsPlayer && !Diplomacy.Hostile(s.Faction) && Vassals.At(s.ID, out _) == null).ToList() ?? new List<RimWorld.Planet.Settlement>();
            Menu(towns.Select(s => new FloatMenuOption(s.Name, () =>
            {
                NotableBook.StaffVassal(s);
                Vassals.Ask(home, s.ID, s.Faction, VassalAsk.Protection, s.Tile);
            })).ToList(), "(no town at peace in reach)");
        }

        [DebugAction("Rimfolk", "Vassals: a town taken under protection at once...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTake()
        {
            var home = Home;
            var reach = RegionTowns.Of(home);
            var towns = reach?.towns.Where(s => s?.Faction != null && !s.Faction.IsPlayer && !Diplomacy.Hostile(s.Faction) && Vassals.At(s.ID, out _) == null).ToList() ?? new List<RimWorld.Planet.Settlement>();
            Menu(towns.Select(s => new FloatMenuOption(s.Name, () => Vassals.Take(home, s))).ToList(), "(no town at peace in reach)");
        }

        [DebugAction("Rimfolk", "Vassals: an ask now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAsk()
        {
            var home = Home;
            var options = new List<FloatMenuOption>();
            foreach (var v in Vassals.Of(home))
                foreach (var ask in new[] { VassalAsk.Help, VassalAsk.Judgement, VassalAsk.Camp, VassalAsk.Release })
                {
                    var vv = v;
                    var a = ask;
                    options.Add(new FloatMenuOption(Vassals.NameOf(v) + ": " + a, () =>
                    {
                        var t = Vassals.TownOf(vv);
                        if (t != null) Vassals.Ask(home, vv.town, vv.people, a, t.Tile);
                    }));
                }
            foreach (var o in Vassals.Outposts(home))
                foreach (var ask in new[] { VassalAsk.Help, VassalAsk.Judgement, VassalAsk.Camp, VassalAsk.Defend })
                {
                    var oo = o;
                    var a = ask;
                    options.Add(new FloatMenuOption((o.name ?? "?") + ": " + a, () =>
                    {
                        NotableBook.StaffOutpost(oo.thing, oo.people, oo.name);
                        Vassals.Ask(home, oo.id, oo.people, a, oo.tile);
                    }));
                }
            Menu(options, "(no vassals at this home)");
        }

        [DebugAction("Rimfolk", "Vassals: a protected town's harvest now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHarvest()
        {
            int n = 0;
            foreach (var v in Vassals.Of(Home))
                if (v.kind == VassalKind.Protected)
                {
                    v.harvestAt = -1;
                    n++;
                }
            Say(n > 0 ? "their carters come at the next day's look in autumn (Septober where the seasons never turn)." : "no protected town at this home.");
        }

        [DebugAction("Rimfolk", "Vassals: levies due now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugVassalLevies()
        {
            int now = Find.TickManager.TicksGame;
            int n = 0;
            foreach (var v in Vassals.Of(Home))
            {
                v.leviesAt = now;
                v.leviesDue = now;
                n++;
            }
            Say(n > 0 ? "their bands walk in within the hour (with a threat on the map), or turn back with word that the fight is over." : "no vassals at this home.");
        }

        [DebugAction("Rimfolk", "Vassals: a season passed (tribute's weight, dues and asks due)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSeason()
        {
            int back = (int)(FealtyMath.SeasonDays * FealtyMath.DayTicks);
            foreach (var v in Vassals.Of(Home))
            {
                v.seasonAt -= back;
                v.duesAt = v.duesAt >= 0 ? v.duesAt - back : -1;
                v.askAt = v.askAt >= 0 ? v.askAt - back : -1;
                v.leviesAt = v.leviesAt >= 0 ? v.leviesAt - back : -1;
            }
            Say("a season moved back on every vassal of this home; the next day's look sees to it.");
        }
    }
}
