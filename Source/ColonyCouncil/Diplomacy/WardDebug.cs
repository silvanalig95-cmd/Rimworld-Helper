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
    /// Dev mode: force each part of wards and hostages at the home on screen (Debug actions, "Rimfolk"): their offer of a
    /// ward, a ward agreed and due now, today's lesson due, the year's end now; a youth of the colony's agreed and setting
    /// out, at their town now, a letter from them, home now; a hostage either way for a live truce or pact, their side
    /// breaking the terms, the colony breaking its word, the term over. Where it goes from there plays by the rules.
    /// </summary>
    public static class WardDebug
    {
        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static int Now => Find.TickManager.TicksGame;

        private static void Say(string text) => Messages.Message("Wards and hostages: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Menu(List<FloatMenuOption> options, string none)
        {
            if (options.Count == 0) options.Add(new FloatMenuOption(none, null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static IEnumerable<Faction> AtPeace() =>
            Find.FactionManager.AllFactionsListForReading.Where(f => f != null && !f.IsPlayer && Diplomacy.Deals(f) && !Diplomacy.Hostile(f));

        [DebugAction("Rimfolk", "Wards: their offer of a ward now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOffer()
        {
            var home = Home;
            Menu(AtPeace().Select(f => new FloatMenuOption(f.Name, () =>
            {
                var book = DiplomacyBook.Instance;
                if (book == null) return;
                if (Wards.KnownFor(home) == null || Wards.Kin(f, home, out _) == null)
                {
                    Say("no guardian with 10 in a skill, or no notable of theirs known");
                    return;
                }
                Wards.Offer(book.HomeOf(home), home, f);
            })).ToList(), "no people at peace");
        }

        [DebugAction("Rimfolk", "Wards: their ward arriving now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugArrive()
        {
            var home = Home;
            Menu(AtPeace().Select(f => new FloatMenuOption(f.Name, () =>
            {
                var w = Wards.TheirsAt(home) ?? Wards.Agree(home, f, Wards.Kin(f, home, out _), Wards.KnownFor(home)?.defName, false, 0.01f);
                if (w == null)
                {
                    Say("couldn't agree a ward (one under way, or nobody of theirs to come)");
                    return;
                }
                w.due = Now;
                Say(w.name + " due now");
            })).ToList(), "no people at peace");
        }

        [DebugAction("Rimfolk", "Wards: today's lesson due", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLesson()
        {
            var w = Wards.TheirsAt(Home);
            if (w == null || w.stage != WardStage.Here)
            {
                Say("no ward here");
                return;
            }
            w.lessonDay = -1;
            Say("a lesson is due for " + w.name + (w.guardian == null ? " (no guardian yet)" : " with " + w.guardian.LabelShort));
        }

        [DebugAction("Rimfolk", "Wards: their ward's year over now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugYearEnd()
        {
            var w = Wards.TheirsAt(Home);
            if (w == null || w.stage != WardStage.Here)
            {
                Say("no ward here");
                return;
            }
            w.until = Now;
            Say(w.name + "'s year ends within the hour");
        }

        [DebugAction("Rimfolk", "Wards: a youth of ours to them now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSend()
        {
            var home = Home;
            var youths = WardsAbroad.Youths(home);
            if (youths.Count == 0)
            {
                Say("no youth at this home could go");
                return;
            }
            Menu(AtPeace().Where(f => WardsAbroad.TownFor(f, home) != null).Select(f => new FloatMenuOption(f.Name, () =>
            {
                var w = WardsAbroad.Agree(home, f, youths[0], true, 0f);
                Say(w == null ? "couldn't agree (one away already)" : w.name + " sets out within the hour");
            })).ToList(), "no people at peace with a town");
        }

        [DebugAction("Rimfolk", "Wards: ours at their town now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugThere()
        {
            var w = DiplomacyBook.Instance?.Existing(Home)?.wards.FirstOrDefault(x => x != null && !x.Theirs && x.stage == WardStage.Away && x.since < 0);
            var c = w?.pawn?.GetCaravan();
            var town = WardsAbroad.TownOf(w);
            if (c == null || town == null)
            {
                Say("nobody of ours on the road to a town");
                return;
            }
            c.Tile = town.Tile;
            c.pather.StopDead();
            Say(w.name + " reaches " + town.Name + " within the hour");
        }

        [DebugAction("Rimfolk", "Wards: a letter from ours due", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLetter()
        {
            var w = DiplomacyBook.Instance?.Existing(Home)?.wards.FirstOrDefault(x => x != null && x.kind == WardKind.Ours && WardsAbroad.There(x));
            if (w == null)
            {
                Say("no youth of ours at a town");
                return;
            }
            w.since = Now - Mathf.RoundToInt((w.letters + 1) * WardMath.LetterDays * WardMath.DayTicks);
            w.until = Mathf.Max(w.until, Now + WardMath.DayTicks);
            Say("a letter from " + w.name + " within the hour");
        }

        [DebugAction("Rimfolk", "Wards: ours home now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHome()
        {
            var w = DiplomacyBook.Instance?.Existing(Home)?.wards.FirstOrDefault(x => x != null && x.kind == WardKind.Ours && WardsAbroad.There(x));
            if (w == null)
            {
                Say("no youth of ours at a town");
                return;
            }
            w.since = Now - Mathf.RoundToInt(WardMath.YearDays * WardMath.DayTicks);
            w.until = Now;
            Say(w.name + " sets out for home within the hour");
        }

        private static IEnumerable<Tie> LiveTerms(Map home) =>
            Truces.Live().Where(t => Truces.Sworn(t) && Truces.HomeOf(t) == home)
                .Concat(WorldLedger.LiveTies(Tie.Kind.Pact).Where(t => t.origin == PactMath.PactOrigin && Pacts.HomeOf(t) == home));

        [DebugAction("Rimfolk", "Hostages: a hostage for a live truce or pact now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGive()
        {
            var home = Home;
            var options = new List<FloatMenuOption>();
            foreach (var t in LiveTerms(home))
            {
                var tie = t;
                var f = Truces.PeopleOf(t) ?? Refs.Faction(t.from.kind == RefKind.People ? t.from : t.to);
                if (f == null) continue;
                options.Add(new FloatMenuOption(t.kind + " with " + f.Name + ": theirs", () =>
                {
                    Say(t.kind == Tie.Kind.Pact ? Hostages.ForPact(f, home, Pacts.KindOf(tie), WardMath.HostageCode(false)) : Hostages.TruceTheirs(tie, f, home));
                    foreach (var w in Hostages.For(tie).Where(x => x.Theirs)) w.due = Now;
                }));
                var c = Hostages.OurCandidates(home).FirstOrDefault();
                if (c != null)
                    options.Add(new FloatMenuOption(t.kind + " with " + f.Name + ": ours (" + c.LabelShort + ")", () =>
                        Say(t.kind == Tie.Kind.Pact ? Hostages.ForPact(f, home, Pacts.KindOf(tie), WardMath.HostageCode(true, c.thingIDNumber)) : Hostages.TruceOurs(tie, f, home, c.thingIDNumber))));
            }
            Menu(options, "no truce sworn or pact live at this home");
        }

        [DebugAction("Rimfolk", "Hostages: their side breaks the terms (forfeit)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugForfeit()
        {
            var h = DiplomacyBook.Instance?.Existing(Home);
            var w = h?.wards.FirstOrDefault(x => x != null && x.kind == WardKind.TheirHostage && x.stage == WardStage.Here);
            var t = Truces.Get(w?.guarantees ?? -1);
            if (w == null || t == null || !t.Live)
            {
                Say("no hostage of theirs here for a live truce");
                return;
            }
            t.terms.goods = Truces.BrokenByThemKey;
            WorldLedger.Set(t, Tie.State.Broken);
            Say("the truce is broken by them; the letter comes within the hour");
        }

        [DebugAction("Rimfolk", "Hostages: the colony breaks its word (held)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHeld()
        {
            var h = DiplomacyBook.Instance?.Existing(Home);
            var w = h?.wards.FirstOrDefault(x => x != null && x.kind == WardKind.OurHostage && WardsAbroad.There(x));
            var t = WorldLedger.Get(w?.guarantees ?? -1);
            if (w == null || t == null || !t.Live)
            {
                Say("no hostage of ours at their town for a live truce or pact");
                return;
            }
            if (t.kind == Tie.Kind.Truce) t.terms.goods = Truces.BrokenByUsKey;
            WorldLedger.Set(t, Tie.State.Broken);
            Say("the colony broke its word; " + w.name + " is held within the hour");
        }

        [DebugAction("Rimfolk", "Hostages: the term over now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTermOver()
        {
            var h = DiplomacyBook.Instance?.Existing(Home);
            var w = h?.wards.FirstOrDefault(x => x != null && x.Hostage && (x.stage == WardStage.Here || WardsAbroad.There(x)));
            var t = WorldLedger.Get(w?.guarantees ?? -1);
            if (w == null || t == null || !t.Live)
            {
                Say("no hostage under a live truce or pact");
                return;
            }
            if (t.kind == Tie.Kind.Truce) Truces.End(t, Tie.State.Ended, null);
            else Pacts.End(t, Tie.State.Ended, null);
            Say("the " + t.kind.ToString().ToLowerInvariant() + " ended; the hostages go home within the hour");
        }
    }
}
