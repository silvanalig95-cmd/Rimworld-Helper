using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colonies on the region map's Ours lens and their cards (docs/design/stage3-many-colonies.md, 3c; the player's
    /// interface decision 7 of 4 November 2026: no page of their own). Each home's row and card say its people (grown-ups
    /// and children), its speaker, mood and strain, wealth, its next day and a threat now; a founding under way shows
    /// where it is going; the colonies of the past (from the ledger's record of every colony) say when they were founded
    /// and how they ended. A colony's card has the acts: go there, make it the seat (decision 1A), found a new colony
    /// (a fresh start), hand it over (Colonies/HandOvers.cs), and, with one under way, call it off or plan its party's journey.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ColonyCards
    {
        static ColonyCards()
        {
            CardSections.HomeLines(CardSlot.Now, 5f, home => Safe("a colony's card", () => Lines(home)), "CC_Colonies_CardHead", "colony");
            CardActs.Register(CardKind.Place, _ => "CC_Colonies_GoThere".Translate().Resolve(), r => CardSections.HomeOf(r) == Find.CurrentMap ? "CC_Colonies_InView".Translate().ToString() : null,
                r => GoTo(CardSections.HomeOf(r)), r => CardSections.HomeOf(r) != null && Homes.All.Count > 1, 10f, "CC_Colonies_GoThereTip".Translate(), "colony-go");
            CardActs.Register(CardKind.Place, r => "CC_Seat_Act".Translate(ColonySeat.Word(CardSections.HomeOf(r)).Named("SEAT")).Resolve(), r => ColonySeat.WhyNot(CardSections.HomeOf(r)),
                r => ColonySeat.Move(CardSections.HomeOf(r)), r => CardSections.HomeOf(r) != null && Homes.All.Count > 1, 11f, "CC_Seat_ActTip".Translate(), "colony-seat");
            CardActs.Register(CardKind.Place, _ => "CC_Found_Act".Translate().Resolve(), r => Foundings.WhyNot(CardSections.HomeOf(r)), r => FoundingPlace.Pick(CardSections.HomeOf(r)),
                r => Foundings.On && CardSections.HomeOf(r) != null && Foundings.Current == null, 12f, "CC_Found_ActTip".Translate(), "colony-found");
            // In a new game's first days: the same colony on a new map (Colonies/Landing.cs).
            CardActs.Register(CardKind.Place, _ => Landing.Label, _ => Landing.WhyNot(), _ => Dialog_Landing.Open(), r => Landing.ShowsFor(CardSections.HomeOf(r)), 12.5f,
                "CC_Landing_CardTip".Translate(), "colony-landing");
            CardActs.Register(CardKind.Place, _ => "CC_Found_ActJourney".Translate().Resolve(), r => JourneyWhyNot(CardSections.HomeOf(r)), r => Foundings.PlanJourney(Under(CardSections.HomeOf(r))),
                r => NeedsJourney(CardSections.HomeOf(r)), 13f, "CC_Found_ActJourneyTip".Translate(), "colony-found-journey");
            CardActs.Register(CardKind.Place, _ => "CC_Found_ActCallOff".Translate().Resolve(), null, r => AskCallOff(Under(CardSections.HomeOf(r))),
                r => Under(CardSections.HomeOf(r)) != null, 14f, "CC_Found_ActCallOffTip".Translate(), "colony-found-off");
            CardActs.Register(CardKind.Place, _ => "CC_Found_ActNewcomers".Translate().Resolve(), null, r => Find.WindowStack.Add(new Dialog_Newcomers(Under(CardSections.HomeOf(r)))),
                r => Under(CardSections.HomeOf(r)) is FoundingPlan f && f.means == FoundingMeans.Newcomers && f.newcomers && !f.newcomersChosen, 13.5f, "CC_Found_ActNewcomersTip".Translate(),
                "colony-found-newcomers");
            // Handing it over (HandOver, Colonies/HandOvers.cs): the window, and with one under way, calling it off.
            CardActs.Register(CardKind.Place, _ => "CC_HandOver_Act".Translate().Resolve(), r => HandOvers.WhyNot(CardSections.HomeOf(r)), r => HandOvers.Open(CardSections.HomeOf(r)),
                r => HandOvers.On && CardSections.HomeOf(r) != null && HandOvers.At(CardSections.HomeOf(r)) == null && HandOvers.Current == null, 12.2f, "CC_HandOver_ActTip".Translate(),
                "colony-handover");
            CardActs.Register(CardKind.Place, _ => "CC_HandOver_ActGoAhead".Translate().Resolve(), null, r => HandOvers.GoAhead(HandOvers.At(CardSections.HomeOf(r))),
                r => HandOvers.At(CardSections.HomeOf(r))?.stage == HandOverStage.Voted, 14.1f, "CC_HandOver_ActGoAheadTip".Translate(), "colony-handover-go");
            CardActs.Register(CardKind.Place, _ => "CC_HandOver_ActCallOff".Translate().Resolve(), null, r => AskCallOffHandOver(HandOvers.At(CardSections.HomeOf(r))),
                r => HandOvers.At(CardSections.HomeOf(r)) != null, 14.2f, "CC_HandOver_ActCallOffTip".Translate(), "colony-handover-off");
        }

        private static List<string> Safe(string what, Func<List<string>> f)
        {
            try
            {
                return f();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colonies: " + what, e);
                return null;
            }
        }

        /// <summary>The founding under way from this home, or null.</summary>
        private static FoundingPlan Under(Map home)
        {
            var f = Foundings.Current;
            return f != null && home != null && f.from == Homes.KeyOf(home) ? f : null;
        }

        private static bool NeedsJourney(Map home)
        {
            var f = Under(home);
            return f != null && f.means == FoundingMeans.Caravan && !JourneyBook.Plans.Any(j => j.founding == f.id) && f.party.Any(p => p != null && p.Spawned && p.Map == home);
        }

        private static string JourneyWhyNot(Map home) =>
            !JourneyBook.On ? "CC_JourneysPg_Off".Translate().ToString() : JourneyBook.PlanAt(home) != null ? "CC_Found_JourneyPlanned".Translate().ToString() : null;

        private static void AskCallOffHandOver(HandOverPlan h)
        {
            if (h == null) return;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_HandOver_CallOffConfirm".Translate((h.homeName ?? "?").Named("HOME")).Resolve(), () => HandOvers.CallOff(h)));
        }

        private static void AskCallOff(FoundingPlan f)
        {
            if (f == null) return;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CC_Found_CallOffConfirm".Translate(f.place.Named("PLACE")).Resolve(), () => Foundings.CallOff(f)));
        }

        /// <summary>Go there: the map in view changes (and every page with it).</summary>
        public static void GoTo(Map home)
        {
            if (home == null || !Homes.IsHome(home)) return;
            Current.Game.CurrentMap = home;
            CameraJumper.TryHideWorld();
            Find.WindowStack.Windows.OfType<Window_Card>().ToList().ForEach(w => w.Close(false));
        }

        // ---------------- What a colony's row and card say ----------------

        /// <summary>Grown-ups and children, its speaker, mood and strain: the row's line.</summary>
        public static string RowLine(Map home)
        {
            var people = Homes.People(home);
            int grown = people.Count(p => p.DevelopmentalStage.Adult());
            int children = people.Count - grown;
            var parts = new List<string> { (children > 0 ? "CC_Colonies_People" : "CC_Colonies_Grown").Translate(grown.Named("GROWN"), children.Named("CHILDREN")).Resolve() };
            var speaker = ColonyState.For(home)?.offices?.Speaker;
            if (speaker != null) parts.Add("CC_Colonies_Speaker".Translate(ThingRefs.Link(speaker.LabelShort, ThingRefs.Of(speaker)).Named("NAME")).Resolve());
            var moods = people.Where(p => p.needs?.mood != null).ToList();
            if (moods.Count > 0) parts.Add("CC_Colonies_Mood".Translate(Mathf.RoundToInt(moods.Average(p => p.needs.mood.CurLevelPercentage) * 100f).Named("MOOD")).Resolve());
            if (Heart.On && people.Count > 0) parts.Add(HeartView.BandLabel(HeartMath.BandFor(Heart.ColonyStrain(people))));
            return string.Join(" · ", parts);
        }

        /// <summary>The card's lines: founded when and from where, the seat or not, its people, wealth, its next day, a threat now, a founding under way.</summary>
        public static List<string> Lines(Map home)
        {
            var lines = new List<string>();
            if (home == null) return lines;
            int key = Homes.KeyOf(home);
            var rec = WorldLedger.ColonyRecord(key);
            if (rec != null && rec.mother != HomesMath.None)
                lines.Add("CC_Colonies_FoundedFrom".Translate(FactionMemory.AgoText(rec.founded).Named("AGO"), Link(rec.mother, rec.motherName).Named("MOTHER")).Resolve());
            if (Homes.All.Count > 1)
                lines.Add(home == Homes.Seat ? "CC_Colonies_IsSeat".Translate(ColonySeat.Word(home).Named("SEAT")).Resolve() : "CC_Colonies_NotSeat".Translate(Homes.Name(Homes.Seat).Named("SEAT")).Resolve());
            lines.Add(RowLine(home));
            lines.Add("CC_Colonies_Wealth".Translate(Mathf.RoundToInt(home.wealthWatcher.WealthTotal).ToString("#,0").Named("WEALTH")).Resolve());
            string next = NextDay(home);
            if (next != null) lines.Add(next);
            if (MapFacts.ThreatOn(home)) lines.Add("CC_Lens_ThreatNow".Translate().Resolve());
            var f = Under(home);
            if (f != null) lines.Add(FoundingLine(f));
            foreach (var child in WorldLedger.Colonies.Where(c => c.mother == key && c.key != key))
                lines.Add("CC_Colonies_FoundedHere".Translate(Link(child.key, child.name).Named("PLACE"), FactionMemory.AgoText(child.founded).Named("AGO")).Resolve());
            return lines;
        }

        /// <summary>A colony by name, linked to its card.</summary>
        private static string Link(int key, string name) => ThingRefs.Link(name ?? "?", ThingRef.From(Ref.Colony(key)));

        /// <summary>The colony's next day on its calendar: its name and date (no countdown).</summary>
        private static string NextDay(Map home)
        {
            var cal = ColonyCalendar.For(home);
            if (cal == null || cal.days == null) return null;
            int today = GenLocalDate.DayOfYear(home);
            var d = cal.days.Where(x => x.Dated && !x.Personal).OrderBy(x => CalendarMath.DaysUntil(today, x.day)).FirstOrDefault();
            return d == null ? null : "CC_Colonies_NextDay".Translate(d.label.Named("DAY"), ColonyCalendar.DateText(d.day, home).Named("DATE")).Resolve();
        }

        /// <summary>A founding under way, in a line: where, who, by what means, and how far along.</summary>
        public static string FoundingLine(FoundingPlan f)
        {
            string who = f.party.Count > 0 ? Foundings.Names(f.party) : "CC_Found_Newcomers".Translate().Resolve();
            string stage;
            var j = JourneyBook.Plans.FirstOrDefault(x => x.founding == f.id);
            if (j != null) stage = "CC_Colonies_FoundingPlanned".Translate(JourneyRun.When(j).Named("WHEN")).Resolve();
            else if (f.party.Any(p => p != null && p.GetCaravan() != null)) stage = "CC_Colonies_FoundingOnTheWay".Translate().Resolve();
            else if (f.means == FoundingMeans.Caravan) stage = "CC_Colonies_FoundingNoJourney".Translate().Resolve();
            else stage = ("CC_Colonies_FoundingBy_" + f.means).Translate().Resolve();
            return "CC_Colonies_Founding".Translate(f.place.Named("PLACE"), who.Named("NAMES"), stage.Named("STAGE")).Resolve();
        }

        /// <summary>
        /// This week's Coming (UI/InboxProviders.cs): a founding under way and a hand-over under way, each where it stands, in
        /// the line its colony's card has (no timer); a click opens the colonies on the region map's Ours lens.
        /// </summary>
        public static void WeekRows(List<WeekRow> rows)
        {
            var h = Homes.Instance;
            if (h == null) return;
            foreach (var f in h.foundings)
            {
                if (f == null || f.stage != FoundingStage.Going) continue;
                rows.Add(new WeekRow
                {
                    icon = Icons.Colony, color = UiKit.Info, text = FoundingLine(f), tip = "CC_Many_WeekColoniesTip".Translate().Resolve(),
                    click = WeekModel.Page("Map/Ours/List"), order = 0
                });
            }
            foreach (var plan in h.handOvers)
            {
                if (plan == null) continue;
                rows.Add(new WeekRow
                {
                    icon = Icons.Colony, color = UiKit.Info, text = HandOvers.Line(plan), tip = "CC_Many_WeekColoniesTip".Translate().Resolve(),
                    click = WeekModel.Page("Map/Ours/List"), order = 0
                });
            }
        }

        /// <summary>A colony of the past, in a line: founded when, and how and when it ended.</summary>
        public static string PastLine(LedgerColony c) =>
            ("CC_Colonies_Ended_" + c.end).Translate(FactionMemory.AgoText(c.founded).Named("FOUNDED"), FactionMemory.AgoText(c.endedAt).Named("ENDED")).Resolve();

        // ---------------- The Ours lens ----------------

        /// <summary>The Ours lens's group for the colonies of the past (after homes, hamlets and caravans).</summary>
        public const int PastGroup = 3;

        /// <summary>
        /// The rows the Ours lens adds: every home beyond the region map's reach (those within it are rows already), a
        /// founding under way, and the colonies of the past. <paramref name="shown"/>: the homes already listed.
        /// </summary>
        public static void OursRows(ICollection<int> shown, Action<int, LensRow> put)
        {
            foreach (var home in Homes.All)
            {
                if (home.Parent == null || shown.Contains(home.Parent.ID)) continue;
                var r = new LensRow { place = home.Parent, thing = ThingRefs.Of(home.Parent), tile = home.Tile, icon = Icons.Colony, tone = UiKit.Gold };
                r.name = ThingRefs.Link(home.Parent.LabelCap, r.thing);
                HomeRow(r, home, false);
                r.note = CardWords.DistanceShort(home.Tile);
                r.order = new RegionViewsMath.RowOrder(RegionViewsMath.Group(Lens.Ours, RegionViewsMath.RowKind.Home),
                    RegionViewsMath.Key(Lens.Ours, RegionViewsMath.RowKind.Home, -1, 0, 0f, home == Homes.Seat), 9999f, r.name);
                put(r.order.group, r);
            }
            var f = Foundings.Current;
            if (f != null)
            {
                var r = new LensRow { thing = ThingRefs.OfTile(f.Tile), tile = f.Tile, icon = Icons.LensOurs, tone = UiKit.Gold, name = f.place };
                r.state = "CC_Colonies_OnItsWay".Translate();
                r.stateColor = UiKit.Warn;
                r.line = FoundingLine(f);
                r.order = new RegionViewsMath.RowOrder(RegionViewsMath.Group(Lens.Ours, RegionViewsMath.RowKind.Home), 50f, 0f, r.name);
                put(r.order.group, r);
            }
            foreach (var c in WorldLedger.Colonies.Where(c => !c.Standing))
            {
                var thing = ThingRef.From(Ref.Colony(c.key));
                var ruin = c.left >= 0 ? Find.WorldObjects.AllWorldObjects.FirstOrDefault(o => o != null && o.ID == c.left) : null;
                var r = new LensRow
                {
                    thing = thing, place = ruin, tile = c.tile >= 0 ? new PlanetTile(c.tile) : PlanetTile.Invalid, icon = Icons.History, tone = UiKit.Faint,
                    name = ThingRefs.Link(c.name ?? "?", thing), state = ("CC_Colonies_EndedWord_" + c.end).Translate(), stateColor = UiKit.Dim, line = PastLine(c)
                };
                r.order = new RegionViewsMath.RowOrder(PastGroup, -c.endedAt, 0f, c.name ?? "");
                put(PastGroup, r);
            }
        }

        /// <summary>A home's row: home or the seat, and its people, speaker, mood and strain; a threat now.</summary>
        public static void HomeRow(LensRow r, Map map, bool threatened)
        {
            r.state = map == Homes.Seat && Homes.All.Count > 1 ? "CC_Lens_Seat".Translate() : "CC_Lens_Home".Translate();
            r.stateColor = UiKit.Gold;
            r.faction = null;
            if (map == null) return;
            r.line = RowLine(map);
            if (threatened || MapFacts.ThreatOn(map)) r.line += " · " + "CC_Lens_ThreatNow".Translate().Resolve();
            var tip = Lines(map).Select(LinkTextMath.Plain).ToList();
            r.tip = string.Join("\n", tip);
        }
    }
}
