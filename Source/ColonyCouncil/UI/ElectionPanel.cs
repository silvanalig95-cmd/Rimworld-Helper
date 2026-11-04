using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The election under way, on the Decrees page above the seats: where it stands and when, each seat's candidates with
    /// what they run on and how support leans (the poll: the vote math without the luck of the day), the poll day by day,
    /// and the council's hand (back a candidate, call a seat's vote off) until the polls open.
    /// </summary>
    public static class ElectionPanel
    {
        private const float RowH = 30f;

        /// <summary>Draws the election, if one is under way; returns the height used (0 with none).</summary>
        public static float Draw(CouncilGameComponent comp, float width, float y)
        {
            // The election of the colony in view.
            var offices = comp == null ? null : (ColonyView.Current?.offices ?? comp.offices);
            var e = offices?.election;
            if (e == null || e.map == null) return 0f;
            float start = y;
            string title = (e.ceremony ? "CC_Elect_Panel_Ceremony" : e.byElection ? "CC_Elect_Panel_ByTitle" : "CC_Elect_Panel_Title").Translate(e.year).Resolve();
            y += UiKit.SectionTitle(0f, y, width, title, Icons.Office, UiKit.Gold, OfficeTracker.ElectionDay(e));
            y += UiKit.Paragraph(0f, y, width, Status(offices, e), UiKit.Faint) + 6f;
            bool hand = offices.CouncilMayStepIn;
            int i = 0;
            foreach (var race in e.races.ToList())
            {
                string seat = race.office.Seats > 1 ? "CC_Office_Seat".Translate(race.office.LabelCap, race.seat + 1).Resolve() : race.office.LabelCap.ToString();
                var head = new Rect(0f, y, width, UiKit.RowStandard);
                UiKit.Zebra(head, i++);
                UiKit.LabelFit(new Rect(6f, y, width * 0.4f, UiKit.RowStandard), seat, UiKit.Heading);
                // The council's hand, while it may step in: a row's small acts.
                if (hand)
                {
                    if (UiKit.RowAct(new Rect(width - 130f, y + 3f, 126f, 22f), "CC_Elect_Panel_CallOff".Translate(), null, "CC_Elect_Panel_CallOffTip".Translate()))
                        ElectionMenus.CallOff(race);
                    if (race.Contested && race.endorsed == null
                        && UiKit.RowAct(new Rect(width - 260f, y + 3f, 126f, 22f), "CC_Elect_Panel_Endorse".Translate(), null, "CC_Elect_Panel_EndorseTip".Translate()))
                        ElectionMenus.Endorse(race);
                }
                y += UiKit.RowStandard + 2f;
                if (!race.Contested)
                {
                    var only = race.entries[0];
                    UiKit.PawnChip(12f, y + 2f, only.pawn, 24f, true, 200f, link: true);
                    UiKit.LabelFit(new Rect(220f, y, width - 224f, RowH), "CC_Elect_Panel_Unopposed".Translate(), UiKit.Faint, GameFont.Tiny);
                    y += RowH + 4f;
                    continue;
                }
                var poll = offices.PollOf(race);
                float total = Mathf.Max(0.001f, poll.Sum());
                for (int k = 0; k < race.entries.Count; k++)
                {
                    var en = race.entries[k];
                    UiKit.PawnChip(12f, y + 3f, en.pawn, 24f, true, 170f, link: true);
                    float share = k < poll.Length ? poll[k] / total : 0f;
                    var bar = new Rect(190f, y + 5f, 120f, 8f);
                    UiKit.Bar(bar, share, race.endorsed == en.pawn ? UiKit.Gold : UiKit.Cool);
                    UiKit.LabelFit(new Rect(190f, y + 13f, 120f, 16f), "CC_Elect_Panel_Votes".Translate((k < poll.Length ? poll[k] : 0f).ToString("0.#")).Resolve(), UiKit.Faint, GameFont.Tiny);
                    TooltipHandler.TipRegion(new Rect(190f, y, 120f, RowH), "CC_Elect_Panel_PollTip".Translate());
                    float tx = 320f;
                    if (race.endorsed == en.pawn) tx += UiKit.Pill(tx, y + 7f, "CC_Elect_Panel_Backed".Translate(), UiKit.Gold) + 6f;
                    if (!en.platform.NullOrEmpty()) UiKit.LabelFit(new Rect(tx, y, Mathf.Max(0f, width - tx - 4f), RowH), en.platform, UiKit.Faint, GameFont.Tiny);
                    y += RowH;
                }
                string days = Days(race);
                if (!days.NullOrEmpty()) y += UiKit.Paragraph(12f, y, width - 12f, days, UiKit.Dim, GameFont.Tiny) + 2f;
                y += 4f;
            }
            return y - start;
        }

        /// <summary>Where the election stands, by its dates and places.</summary>
        private static string Status(OfficeTracker offices, Election e)
        {
            string venue = e.venue.NullOrEmpty() ? "CC_Elect_VenueAny".Translate().ToString() : e.venue;
            string day = OfficeTracker.ElectionDay(e);
            if (e.ceremony) return "CC_Elect_Panel_StatusCeremony".Translate(day.Named("DATE"), venue.Named("VENUE")).Resolve();
            switch (e.phase)
            {
                case ElectionPhase.Campaign:
                    return "CC_Elect_Panel_StatusCampaign".Translate(day.Named("DATE"), venue.Named("VENUE")).Resolve();
                case ElectionPhase.Hustings:
                    return (e.gathering ? "CC_Elect_Panel_StatusHustingsOn" : "CC_Elect_Panel_StatusHustings").Translate(day.Named("DATE"), venue.Named("VENUE")).Resolve();
                case ElectionPhase.Polling:
                    int home = MeetingAttendance.Members(e.map).Count(p => !p.DevelopmentalStage.Juvenile() && !p.IsSlave);
                    return "CC_Elect_Panel_StatusPolling".Translate(day.Named("DATE"), venue.Named("VENUE"), e.voted.Count.Named("VOTED"), home.Named("VOTERS")).Resolve();
                default:
                    return (e.gathering ? "CC_Elect_Panel_StatusCountOn" : "CC_Elect_Panel_StatusCount").Translate(day.Named("DATE"), venue.Named("VENUE")).Resolve();
            }
        }

        /// <summary>"Day by day: 3-2, 4-1, 4-1 (Mira, Tom)": the poll in whole votes as noted each day.</summary>
        private static string Days(ElectionRace race)
        {
            if (race.pollDays.Count == 0) return null;
            var ids = race.entries.Select(en => en.pawn.thingIDNumber).ToList();
            var days = race.pollDays.Select(d =>
            {
                var votes = ids.Select(_ => "?").ToArray();
                foreach (var part in d.Split(','))
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2 && int.TryParse(kv[0], out int id) && ids.Contains(id)) votes[ids.IndexOf(id)] = kv[1];
                }
                return string.Join("-", votes);
            }).ToList();
            return "CC_Elect_Panel_Days".Translate(string.Join(", ", days), string.Join(", ", race.entries.Select(en => en.pawn.LabelShort))).Resolve();
        }
    }
}
