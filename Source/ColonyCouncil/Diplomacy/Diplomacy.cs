using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Stage 2's diplomacy, its base (docs/design/stage2-diplomacy.md; release 2a.1): rules and text on records that already
    /// exist. Every bond is a world ledger tie (favours owed both ways: FavoursOwed.cs; the Secret and Scheme kinds reserved
    /// in TieRules); what isn't a tie goes into the history's family "dip" (<see cref="Write"/>); the peoples remember the
    /// new deeds (FactionMemory.Kinds); words come by both sides' ages (Ages.Form: favor, truce, hostage, envoy, levy); the
    /// player is told through Tell; the region window has its Diplomacy and Household pages (DiplomacyPanels.cs), the
    /// region map's town panel a section, the right-click menu a frame for new acts (DiplomacyActs.cs) and the letters one
    /// for new purposes (DiplomacyLetters.cs). What isn't a tie (an offer for a captive, an envoy's stay either way:
    /// release 2a.3) is each home's part, DiplomacyHome, in DiplomacyBook.
    /// <para>No pass of its own: its hour and day ride on the neighbours' (NotableBook, <see cref="Sched.Neighbours"/>),
    /// whose letters and notables it is built on, and do nothing while nothing waits.</para>
    /// </summary>
    public static class Diplomacy
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The "Favours owed and called in" setting: off, no debt is made or called in, and those open run out by their terms.</summary>
        public static bool FavoursOn => ColonyCouncilMod.Settings?.diplomacyFavours ?? true;

        /// <summary>
        /// The hour (from the neighbours' hour): lines waiting for a letter, calls of theirs overdue; truces and pacts (their
        /// envoys, ends, firebrands, bands on their way, calls to arms, a trade pact's season). Once a day their calls and
        /// offers.
        /// </summary>
        public static void Hourly(int now)
        {
            Safe("lines", () => FavoursOwed.Hourly(now));
            Safe("calls", () => FavourAsks.Hourly(now));
            Safe("truces", () => Truces.Hourly(now));
            Safe("pacts", () => Pacts.Hourly(now));
            Safe("calls to arms", () => CallsToArms.FavourHour(now / (float)PactMath.DayTicks));
            // Ransom and the envoy (release 2a.3): offers for captives either way, their envoy's stay, the colony's envoy away.
            Safe("ransom", () => Ransoms.Hourly(now));
            if (DiplomacyBook.Instance?.AnyWaiting ?? false)
            {
                Safe("envoys", () => Envoys.Hourly(now));
                Safe("envoys abroad", () => EnvoysAbroad.Hourly(now));
            }
            if (Sched.FirstHourOfDay(now))
            {
                Safe("day", () => FavourAsks.Daily(now));
                Safe("pacts' day", () => Pacts.Daily(now));
                Safe("envoys' day", () => Envoys.Daily(now));
                Safe("diplomacy's book", () => DiplomacyBook.Instance?.Daily());
            }
        }

        /// <summary>Something the pages, the inbox and the lenses show changed (an act the player took, a tie made or closed): built again soon.</summary>
        public static void Dirty()
        {
            try
            {
                DiplomacyModel.Dirty();
                LensModel.Dirty();
                Inbox.Dirty();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("diplomacy: dirty", e);
            }
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("diplomacy " + what, e);
            }
        }

        /// <summary>
        /// Something of diplomacy that isn't a tie, into the history (its family "dip"): its kind, who took part (the one it
        /// is mostly of first), where, and its finished line. A milestone when it touches the colony's standing.
        /// </summary>
        public static HistoryEntry Write(string kind, List<HistoryActor> actors, Ref place, string line, bool milestone = false)
        {
            if (kind == null || !kind.StartsWith(DiplomacyMath.Family + ":", StringComparison.Ordinal)) return null;
            return History.Add(kind, actors, place, milestone ? HistoryMath.Milestone : HistoryMath.Everyday, line);
        }

        /// <summary>The colony as a party: the home given, else the seat (or the player's people between homes).</summary>
        public static Ref Colony(Map home) => home != null && Homes.IsHome(home) ? Refs.Colony(home) : Refs.Us;

        /// <summary>The home a colony's ref names, else the seat.</summary>
        public static Map HomeOf(Ref colony) => Refs.Home(colony) ?? Homes.Seat;

        /// <summary>A people the diplomacy deals with: one of the world's real peoples (as the memory's), not defeated.</summary>
        public static bool Deals(Faction f) => FactionMemory.Remembers(f) && !f.defeated;

        public static bool Hostile(Faction f) => f != null && f.HostileTo(Faction.OfPlayer);
    }
}
