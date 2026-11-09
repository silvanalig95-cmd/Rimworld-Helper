using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The election's announcement: who stands and on what, and the council's part in it. Let the colony vote, back a
    /// candidate, or call a seat's vote off and appoint someone (a menu of the seats and their candidates, each with what
    /// it costs). The same choices are on the Decrees page while the campaign lasts.
    /// </summary>
    public class ChoiceLetter_Election : RimfolkLetter
    {
        public int year;
        /// <summary>Where the election is held: whose council's it is (an older letter has none: the seat's).</summary>
        public Map map;

        /// <summary>The council whose election this letter is about.</summary>
        private OfficeTracker Tracker => map != null ? OfficeTracker.WithElectionOn(map) ?? OfficeTracker.For(map) : CouncilGameComponent.Instance?.offices;

        /// <summary>The campaign still runs, so the council may still step in (This week's Needs you counts it).</summary>
        public bool Waiting
        {
            get
            {
                if (ArchivedOnly) return false;
                var e = Tracker?.election;
                return e != null && e.year == year && !e.ceremony;
            }
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly)
                {
                    yield return Option_Close;
                    yield break;
                }
                var offices = Tracker;
                var e = offices?.election;
                bool live = e != null && e.year == year && !e.ceremony;
                if (!live)
                {
                    yield return Option_Close;
                    yield break;
                }
                var vote = new DiaOption("CC_Elect_Choice_Vote".Translate())
                {
                    action = () => Find.LetterStack.RemoveLetter(this),
                    resolveTree = true
                };
                // Letting the vote run is kept in the answers' log; backing someone or calling a seat off is recorded where it's done.
                yield return Answers.Recorded(vote, this, "election", map: () => map, minor: true);
                var back = new DiaOption("CC_Elect_Choice_Endorse".Translate())
                {
                    action = () => ElectionMenus.Endorse(null, offices),
                    resolveTree = true
                };
                if (!offices.CouncilMayStepIn) back.Disable("CC_Elect_TooLate".Translate());
                else if (!e.races.Any(r => r.Contested && r.endorsed == null)) back.Disable("CC_Elect_NothingToBack".Translate());
                yield return back;
                var off = new DiaOption("CC_Elect_Choice_CallOff".Translate())
                {
                    action = () => ElectionMenus.CallOff(null, offices),
                    resolveTree = true
                };
                if (!offices.CouncilMayStepIn) off.Disable("CC_Elect_TooLate".Translate());
                yield return off;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref year, "year");
            Scribe_References.Look(ref map, "map");
        }
    }

    /// <summary>The menus for the council's hand in an election: from the letter and from the Decrees page.</summary>
    public static class ElectionMenus
    {
        /// <summary>The council whose election has this race (else the colony in view's).</summary>
        private static OfficeTracker Offices(ElectionRace race)
        {
            var all = CouncilGameComponent.Instance?.colonies;
            if (race != null && all != null)
                foreach (var c in all)
                    if (c?.offices?.election != null && c.offices.election.races.Contains(race)) return c.offices;
            return ColonyState.InView?.offices;
        }

        /// <summary>Back a candidate: every seat with a race the council hasn't backed anyone in yet, each candidate with what it costs.</summary>
        public static void Endorse(ElectionRace only = null, OfficeTracker tracker = null)
        {
            var offices = tracker ?? Offices(only);
            var e = offices?.election;
            var comp = CouncilGameComponent.Instance;
            if (e == null || comp == null || !offices.CouncilMayStepIn) return;
            var options = new List<FloatMenuOption>();
            foreach (var race in e.races.Where(r => r.Contested && r.endorsed == null && (only == null || r == only)))
                foreach (var en in race.entries)
                {
                    var r = race;
                    var p = en.pawn;
                    int against = offices.EndorseCost(r, p);
                    string label = "CC_Elect_Menu_Endorse".Translate(p.LabelShort, r.office.Name, against).Resolve();
                    options.Add(new FloatMenuOption(label, () => offices.Endorse(r, p, comp)));
                }
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>Call a seat's vote off: every seat and each candidate in it, with whose trust it costs.</summary>
        public static void CallOff(ElectionRace only = null, OfficeTracker tracker = null)
        {
            var offices = tracker ?? Offices(only);
            var e = offices?.election;
            var comp = CouncilGameComponent.Instance;
            if (e == null || comp == null || !offices.CouncilMayStepIn) return;
            var options = new List<FloatMenuOption>();
            foreach (var race in e.races.Where(r => only == null || r == only))
            {
                var leader = race.Contested ? offices.PollLeader(race) : race.entries.FirstOrDefault()?.pawn;
                foreach (var en in race.entries)
                {
                    var r = race;
                    var p = en.pawn;
                    int cost = leader != null && leader != p ? offices.Supporters(r, leader).Count(v => v != p) : 0;
                    string label = cost == 0
                        ? "CC_Elect_Menu_AppointFree".Translate(p.LabelShort, r.office.Name).Resolve()
                        : "CC_Elect_Menu_Appoint".Translate(p.LabelShort, r.office.Name, cost, leader.LabelShort).Resolve();
                    options.Add(new FloatMenuOption(label, () => offices.CallOff(r, p, comp)));
                }
            }
            if (options.Count > 0) Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
