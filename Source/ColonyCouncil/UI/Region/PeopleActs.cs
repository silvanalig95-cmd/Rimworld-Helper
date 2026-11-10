using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One act of "Do something with...": its words, what it brings, how it travels, what it costs, why not, and its own code path.</summary>
    public sealed class PeopleActRow
    {
        public PeopleActKind kind;
        public ActGroup group;
        public string label, button, why, tip;
        /// <summary>What it brings (one line, true to what the act does); for a letter by occasion, the reason it fits now.</summary>
        public string brings, reason;
        /// <summary>How it travels and what it costs, in one line.</summary>
        public string how;
        public Action run;
        public float width = -1f, height;
    }

    /// <summary>
    /// "Do something with..." on a people's Overview (docs/design/diplomacy-screen.md, step A, point 3; post-and-letters.md,
    /// step 2, points 10 and 11): three targets (the whole people, one of their towns, one of their people), and for the
    /// one chosen every act Rimfolk already has that fits it, each through its own code path: the letters by occasion
    /// (greyed with "No reason now" when there is none: World/LetterOccasions.cs), the acts registered on that thing's
    /// card (pacts and truces, fealty, envoys, feasts, matches, wards, exchanges, a vassal's levies, a town asked back, a
    /// journey), the debts of honor to call in or answer, and someone of theirs at the colony to talk with face to face.
    /// Each says what it brings (PeopleActMath's keys: written from what the act does), how it travels (the post's reach
    /// line, Post/PostWords.cs) and what it costs; one not open now is greyed with its own check's reason. Built at most once a
    /// second for the target in view, laid out once per width.
    /// </summary>
    public static class PeopleActs
    {
        private static readonly List<PeopleActRow> rows = new List<PeopleActRow>();
        private static float builtAt = -100f;
        private static int builtPeople = int.MinValue, builtTown = int.MinValue, builtPerson = int.MinValue;
        private static ActTarget builtTarget;
        /// <summary>The town and the person aimed at, found with the rows (the pickers' words).</summary>
        private static string townName, personName;

        public static void Dirty() => builtAt = -100f;

        private static Map Home => PeoplesModel.Home;

        // ================= What the target is =================

        /// <summary>The town aimed at: the one chosen, else (gone or none) the nearest.</summary>
        public static Settlement Town(PeopleEntry e)
        {
            foreach (var t in e.towns)
                if (t.s.ID == RegionView.targetTown)
                    return t.s;
            return e.nearest?.s;
        }

        /// <summary>The person aimed at: the one chosen, else their head.</summary>
        public static Notable Person(PeopleEntry e)
        {
            var book = NotableBook.Instance;
            var n = RegionView.targetPerson >= 0 ? book?.Get(RegionView.targetPerson) : null;
            if (n != null && n.Alive && n.faction == e.f) return n;
            return FavourCalls.HeadOf(e.f, Home) ?? e.notables.FirstOrDefault();
        }

        /// <summary>Who of theirs the colony knows by name and alive, closest town first, then head, heir, envoy, trader, faces.</summary>
        public static List<Notable> Notables(PeopleEntry e)
        {
            var book = NotableBook.Instance;
            if (book == null) return new List<Notable>();
            var order = new Dictionary<int, int>();
            for (int i = 0; i < e.towns.Count; i++) order[e.towns[i].s.ID] = i;
            return book.notables.Where(n => n != null && n.faction == e.f && n.Alive)
                .OrderBy(n => order.TryGetValue(n.town, out int i) ? i : 999).ThenBy(n => (int)n.role).ThenBy(n => n.id).ToList();
        }

        /// <summary>Whom a letter to the target goes to: the person; a town's head (or whoever the colony knows there); the people's head.</summary>
        private static Notable LetterTo(PeopleEntry e, Settlement town)
        {
            switch (RegionView.target)
            {
                case ActTarget.Person: return Person(e);
                case ActTarget.Town:
                    var at = e.notables.Where(n => town != null && n.town == town.ID).ToList();
                    return at.FirstOrDefault(n => n.role == NotableRole.Head) ?? at.FirstOrDefault();
                default:
                    return FavourCalls.HeadOf(e.f, Home);
            }
        }

        // ================= Building =================

        /// <summary>The acts for the target in view, built at most once a second.</summary>
        public static List<PeopleActRow> Rows(PeopleEntry e)
        {
            float now = Time.realtimeSinceStartup;
            if (builtPeople == e.f.loadID && builtTarget == RegionView.target && builtTown == RegionView.targetTown && builtPerson == RegionView.targetPerson
                && now - builtAt < 1f && now >= builtAt) return rows;
            builtPeople = e.f.loadID;
            builtTarget = RegionView.target;
            builtTown = RegionView.targetTown;
            builtPerson = RegionView.targetPerson;
            builtAt = now;
            rows.Clear();
            townName = Town(e)?.Name;
            var aimed = Person(e);
            personName = aimed != null ? NotableBook.Called(aimed) : null;
            try
            {
                Build(e);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("region window: a people's acts", ex);
            }
            return rows;
        }

        private static void Build(PeopleEntry e)
        {
            var home = Home;
            var town = RegionView.target == ActTarget.Person ? NotableBook.TownOf(Person(e)?.town ?? -1) ?? e.nearest?.s : Town(e);
            var to = LetterTo(e, town);
            var reachTown = to != null ? NotableBook.TownOf(to.town) ?? town : town;
            string reach = ReachLine(home, reachTown, to);
            // The letters by occasion, to whom the target's letters go.
            foreach (var p in PeopleActMath.Letters) rows.Add(Letter(e, p, to, reach));
            // The acts on the target's card, each through its own code path.
            ThingRef thing;
            CardKind kind;
            switch (RegionView.target)
            {
                case ActTarget.Town:
                    thing = ThingRefs.Of(town);
                    kind = CardKind.Place;
                    break;
                case ActTarget.Person:
                    thing = ThingRefs.Of(Person(e));
                    kind = CardKind.Person;
                    break;
                default:
                    thing = e.thing;
                    kind = CardKind.People;
                    break;
            }
            if (!thing.IsNone)
                foreach (var entry in CardActs.registry.In(kind, CardSlot.Acts))
                {
                    if (PeopleActMath.Elsewhere.Contains(entry.id)) continue;
                    var a = entry.value;
                    try
                    {
                        if (a.shows != null && !a.shows(thing)) continue;
                        var th = thing;
                        var k = PeopleActMath.KindOf(entry.id);
                        rows.Add(Make(k, a.label(th), a.whyNot?.Invoke(th), a.tip, () => a.act(th), reach, home, reachTown));
                    }
                    catch (Exception ex)
                    {
                        CouncilLog.ErrorOnce("region window: the act " + entry.id, ex);
                    }
                }
            // Debts of honor: theirs to the colony to call in, the colony's they called in to answer.
            foreach (var t in Debts(e, town))
            {
                bool ours = FavoursOwed.ColonyOwes(t);
                var act = ours ? DiplomacyModel.AnswerAct(t) : DiplomacyModel.CallAct(t, home);
                if (act == null) continue;
                var row = Make(ours ? PeopleActKind.AnswerDebt : PeopleActKind.CallDebt, act.label, act.why ?? (act.run == null ? act.tip : null), act.tip, act.run, reach, home, reachTown);
                row.label = act.label + ": " + LinkTextMath.Plain(DipWords.DebtLine(t));
                rows.Add(row);
            }
            // Someone of theirs at the colony: talk with them face to face.
            foreach (var p in Here(e))
            {
                if (RegionView.target == ActTarget.Person && to?.pawn != p) continue;
                var pawn = p;
                var row = Make(PeopleActKind.FaceToFace, "CC_PplAct_Face".Translate(p.LabelShort.Named("PAWN")).Resolve(), null, null,
                    () => Cards.ShowOnMap(new GlobalTargetInfo(pawn)), reach, home, reachTown);
                row.button = "CC_PplAct_Face_Button".Translate().Resolve();
                rows.Add(row);
            }
            // A stable sort: the groups in order, each in the order it was built.
            var sorted = rows.Select((r, i) => (r, i)).OrderBy(x => (int)x.r.group).ThenBy(x => x.i).Select(x => x.r).ToList();
            rows.Clear();
            rows.AddRange(sorted);
        }

        /// <summary>The debts of honor that belong to the target: the people's own and its notables' for the people; a town's notables'; the person's.</summary>
        private static List<Tie> Debts(PeopleEntry e, Settlement town)
        {
            var list = new List<Tie>();
            if (!FavoursOwed.On) return list;
            var person = RegionView.target == ActTarget.Person ? Person(e) : null;
            foreach (var t in FavoursOwed.OwedBy(e.f).Concat(FavoursOwed.OwedTo(e.f)))
            {
                var n = FavoursOwed.IsPersonal(t) ? FavoursOwed.NotableOf(t) : null;
                switch (RegionView.target)
                {
                    case ActTarget.Town:
                        if (n == null || town == null || n.town != town.ID) continue;
                        break;
                    case ActTarget.Person:
                        if (n == null || n != person) continue;
                        break;
                }
                list.Add(t);
            }
            return list;
        }

        /// <summary>Someone of theirs at a home of the colony now, not leaving.</summary>
        public static List<Pawn> Here(PeopleEntry e)
        {
            var list = new List<Pawn>();
            foreach (var map in Homes.All)
                foreach (var p in map.mapPawns.AllHumanlikeSpawned)
                    if (Outsiders.IsOutsider(p) && Outsiders.PeopleOf(p) == e.f && !Outsiders.Leaving(p) && !p.DevelopmentalStage.Juvenile())
                        list.Add(p);
            return list;
        }

        /// <summary>A letter by occasion to them: its reason now, or greyed with why (World/LetterOccasions.cs), opening the letter's dialog set to it.</summary>
        private static PeopleActRow Letter(PeopleEntry e, LetterPurpose p, Notable to, string reach)
        {
            var kind = PeopleActMath.KindOf(p);
            string label = ("CC_Letter_PurposeCap_" + p).Translate().Resolve();
            string why;
            if (!NeighbourLetters.On) why = "CC_Letter_Why_Off".Translate().Resolve();
            else if (to == null) why = "CC_PplAct_Why_NoOne".Translate(e.name.Named("PEOPLE")).Resolve();
            else why = LetterOccasions.WhyNot(to, p);
            Action run = null;
            if (why == null && to != null)
            {
                var n = to;
                var topics = p == LetterPurpose.Condolences || p == LetterPurpose.Warning ? NeighbourLetters.Topics(n, p) : null;
                string topic = topics != null && topics.Count > 0 ? topics[0].key : null;
                run = () => Find.WindowStack.Add(new Dialog_NeighbourLetter(n, p, topic));
            }
            var row = Make(kind, label, why, null, run, reach, PeoplesModel.Home, null);
            row.button = (NeighbourLetters.Spoken ? "CC_PplAct_Speak" : "CC_PplAct_Write").Translate().Resolve();
            if (to != null) row.label = "CC_PplAct_LetterTo".Translate(label.Named("LETTER"), NotableBook.Called(to).Named("NAME")).Resolve();
            if (why == null && to != null && p != LetterPurpose.News) row.reason = LetterOccasions.Reason(to, p);
            // A town's matter to someone who isn't its head is passed on to the head (World/LetterRoutes.cs): it says to whom.
            if (why == null && to != null && LetterRoutes.Way(to, p, LetterAddressMath.ScopeOf(p, false), out var head) == LetterWay.PassOn)
                row.reason = "CC_Route_Row_Pass".Translate(LetterRoutes.Short(head).Named("HEAD")).Resolve().CapitalizeFirst() + (row.reason.NullOrEmpty() ? "" : ". " + row.reason);
            return row;
        }

        /// <summary>An act's row: what it brings and costs by its kind, how it travels by its way.</summary>
        private static PeopleActRow Make(PeopleActKind k, string label, string why, string tip, Action run, string reach, Map home, Settlement town)
        {
            var row = new PeopleActRow { kind = k, group = PeopleActMath.GroupOf(k), label = label, button = label, why = why, tip = tip, run = run };
            if (row.why == null && row.run == null) row.why = tip ?? "";
            bool alt = PeopleActMath.Alt(k, Truces.KeepRaidsAway, FavoursOwed.On);
            string forKey = PeopleActMath.ForKey(k, alt);
            row.brings = k == PeopleActKind.Other ? tip : forKey.CanTranslate() ? forKey.Translate().Resolve() : tip;
            var way = PeopleActMath.Way(k);
            string how = way == ActWay.Letter ? reach : WayLine(way, home, town);
            string costKey = PeopleActMath.CostKey(k, NeighbourLetters.Spoken);
            string cost = costKey != null && costKey.CanTranslate() ? costKey.Translate().Resolve() : null;
            row.how = how.NullOrEmpty() ? cost : cost.NullOrEmpty() ? how : how + " " + cost;
            // The button says the act in a word or two where the label is long (a debt's own line).
            if (row.button != null && row.button.Length > 28) row.button = "CC_PplAct_Do".Translate().Resolve();
            return row;
        }

        /// <summary>How an act that isn't a letter travels, in words.</summary>
        private static string WayLine(ActWay w, Map home, Settlement town)
        {
            string key = PeopleActMath.WayKey(w);
            if (key == null) return null;
            if (w == ActWay.Colonist)
            {
                float walk = home != null && town != null ? PostReach.WalkDays(home.Tile, town.Tile, true) : -1f;
                return (walk >= 0f ? key : key + "_NoRoad").Translate(PeoplesModel.DaysWord(walk).Named("DAYS"), (town?.Name ?? "").Named("TOWN")).Resolve();
            }
            return key.Translate().Resolve();
        }

        /// <summary>How a letter reaches them now: the post's reach line for their town (Post/PostWords.cs), else by their messenger or a call.</summary>
        public static string ReachLine(Map home, Settlement town, Notable to)
        {
            if (home == null) return null;
            var lines = town != null ? PostWords.ReachLines(home, town) : new List<string>();
            if (lines.Count > 0) return lines[0];
            if (to != null && NeighbourLetters.ChannelWhyNot(to, LetterChannel.Console, home) == null) return "CC_PplAct_Way_Call".Translate().Resolve();
            return "CC_PplAct_Way_Messenger".Translate().Resolve();
        }

        // ================= Drawing =================

        private const float ButtonW = 112f;

        /// <summary>
        /// The panel from <paramref name="y"/>: the targets with their pickers, then the acts in their sections (one column,
        /// or two on a wide page), each with its button greyed and why when not open. Returns the y below it.
        /// </summary>
        public static float Draw(PeopleEntry e, float y, float w, float sightTop, float sightBottom)
        {
            var list = Rows(e);
            y = Targets(e, y, w);
            int cols = PeoplesViewMath.ActColumns(w);
            float cw = PeoplesViewMath.ActColumnWidth(w);
            ActGroup? group = null;
            int col = 0;
            float rowTop = y, rowH = 0f;
            foreach (var r in list)
            {
                if (group != r.group)
                {
                    if (col > 0) y = rowTop + rowH + 6f;
                    col = 0;
                    group = r.group;
                    y += UiKit.SectionTitle(0f, y, w, PeopleActMath.GroupKey(r.group).Translate().Resolve());
                    rowTop = y;
                    rowH = 0f;
                }
                float h = Measure(r, cw);
                float x = col * (cw + 12f);
                if (rowTop + h >= sightTop && rowTop <= sightBottom) Row(new Rect(x, rowTop, cw, h), r);
                rowH = Mathf.Max(rowH, h);
                col++;
                if (col >= cols)
                {
                    col = 0;
                    y = rowTop + rowH + 6f;
                    rowTop = y;
                    rowH = 0f;
                }
            }
            if (col > 0) y = rowTop + rowH + 6f;
            return y + 4f;
        }

        /// <summary>The three targets, and the picker of a town (closest first) or a person.</summary>
        private static float Targets(PeopleEntry e, float y, float w)
        {
            var labels = TargetLabels;
            float mw = Mathf.Min(UiKit.ModesWidth(labels), w);
            int pick = UiKit.Modes(new Rect(0f, y, mw, UiKit.ModesHeight), labels, (int)RegionView.target, null, TargetTips);
            if (pick >= 0)
            {
                RegionView.target = (ActTarget)pick;
                Dirty();
            }
            float px = mw + 8f, pw = Mathf.Min(260f, w - px);
            if (pw >= 80f && RegionView.target != ActTarget.People)
            {
                var r = new Rect(px, y, pw, UiKit.ModesHeight);
                if (RegionView.target == ActTarget.Town)
                {
                    if (UiKit.Button(r, UiKit.Fit(townName ?? NoTown, pw - 10f), ButtonKind.Normal, e.towns.Count > 0))
                        Find.WindowStack.Add(new FloatMenu(e.towns.Select(t => new FloatMenuOption(t.s.Name + " (" + (t.daysWord ?? PeoplesModel.DaysWord(-1f)) + ")", () =>
                        {
                            RegionView.targetTown = t.s.ID;
                            Dirty();
                        })).ToList()));
                }
                else
                {
                    var all = e.notables;
                    if (UiKit.Button(r, UiKit.Fit(personName ?? NoPerson, pw - 10f), ButtonKind.Normal, all.Count > 0))
                        Find.WindowStack.Add(new FloatMenu(all.Select(x => new FloatMenuOption(NotableBook.Called(x) + (x.townName.NullOrEmpty() ? "" : ", " + x.townName), () =>
                        {
                            RegionView.targetPerson = x.id;
                            Dirty();
                        })).ToList()));
                }
            }
            return y + UiKit.ModesHeight + 8f;
        }

        private static string[] targetLabels, targetTips;
        private static string noTown, noPerson;
        private static string NoTown => noTown ??= "CC_PplAct_NoTown".Translate();
        private static string NoPerson => noPerson ??= "CC_PplAct_NoPerson".Translate();
        private static string[] TargetLabels => targetLabels ??= new[] { "CC_PplAct_Target_People".Translate().ToString(), "CC_PplAct_Target_Town".Translate().ToString(), "CC_PplAct_Target_Person".Translate().ToString() };
        private static string[] TargetTips => targetTips ??= new[] { "CC_PplAct_Target_People_Tip".Translate().ToString(), "CC_PplAct_Target_Town_Tip".Translate().ToString(), "CC_PplAct_Target_Person_Tip".Translate().ToString() };

        /// <summary>A row's height in a width: the label line, what it brings, and how it travels and costs (or why not), measured once per width.</summary>
        private static float Measure(PeopleActRow r, float w)
        {
            if (Mathf.Abs(r.width - w) < 0.5f) return r.height;
            r.width = w;
            float tw = Mathf.Max(40f, w - ButtonW - 16f);
            float h = 26f;
            if (!r.reason.NullOrEmpty()) h += LinkText.Height(r.reason, tw, GameFont.Tiny);
            if (!r.brings.NullOrEmpty()) h += LinkText.Height(r.brings, w - 12f, GameFont.Tiny);
            string third = r.why ?? r.how;
            if (!third.NullOrEmpty()) h += LinkText.Height(third, w - 12f, GameFont.Tiny);
            r.height = h + 8f;
            return r.height;
        }

        private static void Row(Rect r, PeopleActRow row)
        {
            bool can = row.why == null;
            UiKit.Card(r, can ? (Color?)null : UiKit.WithAlpha(UiKit.Dim, 0.4f), false, can);
            float x = r.x + 6f, y = r.y + 3f, tw = Mathf.Max(40f, r.width - ButtonW - 16f);
            var b = new Rect(r.xMax - ButtonW - 6f, r.y + 4f, ButtonW, 24f);
            if (UiKit.ActButton(b, row.button ?? row.label, can ? null : row.why.NullOrEmpty() ? row.tip ?? "" : row.why, row.tip)) Run(row);
            UiKit.LabelFit(new Rect(x, y, tw, 22f), row.label, can ? Color.white : UiKit.Faint);
            y += 23f;
            if (!row.reason.NullOrEmpty()) y += LinkText.Draw(new Rect(x, y, tw, 999f), row.reason, UiKit.Done, GameFont.Tiny);
            if (!row.brings.NullOrEmpty()) y += LinkText.Draw(new Rect(x, y, r.width - 12f, 999f), row.brings, can ? UiKit.Faint : UiKit.Dim, GameFont.Tiny);
            string third = row.why ?? row.how;
            if (!third.NullOrEmpty()) LinkText.Draw(new Rect(x, y, r.width - 12f, 999f), third, row.why != null ? UiKit.Warn : UiKit.Dim, GameFont.Tiny);
            if (!row.tip.NullOrEmpty() && can) TooltipHandler.TipRegion(new Rect(r.x, r.y, r.width - ButtonW - 10f, r.height), row.tip);
        }

        private static void Run(PeopleActRow row)
        {
            try
            {
                row.run?.Invoke();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region window: a people's act", e);
            }
            DiplomacyModel.Dirty();
            PeoplesModel.Dirty();
            Cards.Dirty();
            Dirty();
        }
    }
}
