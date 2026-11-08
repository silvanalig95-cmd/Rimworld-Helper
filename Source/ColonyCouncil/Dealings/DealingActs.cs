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
    /// Where the dealings are offered and shown: an act for each on a town's card (and so on "Do something with..." under
    /// the town, UI/Region/PeopleActs.cs), the proclamations and petitions on a people's card too (to their leaders as a
    /// whole), "Answer their letter" on a notable's card while a letter they wrote first waits; the town card's lines of
    /// what stands between the colony and that town; the Dealings tab's section (UI/Region/PeoplePage.cs); and the inbox's
    /// letters waiting for an answer. Each act opens the letter's window set to it (World/NeighboursPanel.cs), where the
    /// terms, the writer and the way it goes are chosen.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class DealingActs
    {
        /// <summary>The acts on a town's card, their order and their ids (each the act PeopleActMath.ById names).</summary>
        private static readonly (DealKind kind, float order, string id)[] TownActs =
        {
            (DealKind.Supply, 60f, "deal-supply"), (DealKind.Relief, 61f, "deal-relief"), (DealKind.Proclaim, 62f, "deal-proclaim"), (DealKind.Hire, 63f, "deal-hire"),
            (DealKind.Petition, 64f, "deal-petition"), (DealKind.Work, 65f, "deal-work"), (DealKind.Market, 66f, "deal-market"), (DealKind.Invite, 67f, "deal-invite")
        };

        /// <summary>The acts on a people's card: to their leaders as a whole.</summary>
        private static readonly (DealKind kind, float order, string id)[] PeopleActs =
        {
            (DealKind.Proclaim, 60f, "deal-proclaim-people"), (DealKind.Petition, 61f, "deal-petition-people")
        };

        static DealingActs()
        {
            foreach (var (kind, order, id) in TownActs)
            {
                var k = kind;
                CardActs.Register(CardKind.Place, r => Dealings.W("CC_Deal_Act_" + k), r => WhyNot(k, CardSections.TownOf(r)?.Faction, CardSections.TownOf(r)),
                    r => Open(k, CardSections.TownOf(r)?.Faction, CardSections.TownOf(r)), r => Shows(CardSections.TownOf(r)?.Faction), order,
                    Dealings.W("CC_Deal_Act_" + k + "_Tip"), id);
            }
            foreach (var (kind, order, id) in PeopleActs)
            {
                var k = kind;
                CardActs.Register(CardKind.People, r => Dealings.W("CC_Deal_Act_" + k), r => WhyNot(k, ThingRefs.Faction(r), null), r => Open(k, ThingRefs.Faction(r), null),
                    r => Shows(ThingRefs.Faction(r)), order, Dealings.W("CC_Deal_Act_" + k + "_Tip"), id);
            }
            CardActs.Register(CardKind.Person, r => Dealings.W("CC_Deal_Act_Reply"), r => ReplyWhyNot(ThingRefs.Notable(r)), r => Reply(ThingRefs.Notable(r)),
                r => ThingRefs.Notable(r) is Notable n && WorldWrites.Waiting(n).Count > 0, 5f, Dealings.W("CC_Deal_Act_Reply_Tip"), "deal-reply");
            CardSections.TownLines(CardSlot.Now, 46f, Dealings.TownLines, null, "dealings-town");
            Inbox.Register("dealings-letters", InboxSection.Needs, 35f, NeedRows, () => Dealings.On ? WorldWrites.AllWaiting().Count : 0);
            Inbox.Register("dealings-under-way", InboxSection.UnderWay, 55f, UnderWayRows);
        }

        private static Map Home => HappeningTracker.HomeMap() ?? Homes.Seat;

        private static bool Shows(Faction f) => f != null && !f.IsPlayer && Dealings.On && Diplomacy.Deals(f);

        /// <summary>Why none of this dealing's kinds can be asked of them now (the first one's reason, or the letters' own: one at a time to each), or null.</summary>
        public static string WhyNot(DealKind k, Faction f, Settlement town)
        {
            var home = Home;
            Dealings.FirstOpen(k, f, town, home, out string why);
            if (why != null) return why;
            return LetterWhyNot(Dealings.Receiver(f, town, home));
        }

        /// <summary>The letters' own rules for the one who receives it: alive, not here, no letter to them on its way already, not too many open.</summary>
        public static string LetterWhyNot(Notable n)
        {
            if (n == null || !n.Alive) return "CC_Letter_Why_Gone".Translate();
            if (NeighbourLetters.OpenTo(n) != null) return "CC_Letter_Why_OneAtATime".Translate();
            int max = PostBook.On ? PostMath.MaxOpen : NotableMath.MaxOpen;
            if (NeighbourLetters.OpenCount >= max) return "CC_Letter_Why_TooMany".Translate(max).Resolve();
            if (n.pawn != null && n.pawn.Spawned) return "CC_Letter_Why_Here".Translate(n.Name.Named("NAME")).Resolve();
            return null;
        }

        /// <summary>The letter's window to their council, set to the first of this dealing's kinds that is open now.</summary>
        public static void Open(DealKind k, Faction f, Settlement town)
        {
            var home = Home;
            byte s = Dealings.FirstOpen(k, f, town, home, out string why);
            var n = Dealings.Receiver(f, town, home);
            if (why != null || n == null) return;
            Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Dealing, DealingMath.TopicKey(k, s, town?.ID ?? -1)));
        }

        public static string ReplyWhyNot(Notable n)
        {
            if (n == null || WorldWrites.Waiting(n).Count == 0) return Dealings.W("CC_Deal_Why_NoLetter");
            return LetterWhyNot(n);
        }

        public static void Reply(Notable n)
        {
            var a = n != null ? WorldWrites.Waiting(n).FirstOrDefault() : null;
            if (a == null || ReplyWhyNot(n) != null) return;
            Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Dealing, DealingLetters.ReplyPrefix + a.id));
        }

        // ================= The inbox =================

        private static void NeedRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null || !Dealings.On) return;
            try
            {
                foreach (var a in WorldWrites.AllWaiting())
                {
                    var n = NotableBook.Instance?.Get(a.notable);
                    if (n == null || !n.Alive) continue;
                    var who = n;
                    string why = ReplyWhyNot(who);
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Promise ?? Icons.Trust, color = UiKit.Gold, faction = n.faction,
                        text = Dealings.W("CC_Deal_Week_Letter", ThingRefs.Link(n.Name, ThingRefs.Of(n)).Named("NAME"), (n.townName ?? n.faction?.Name ?? "?").Named("TOWN")),
                        sub = Dealings.W("CC_Deal_Week_" + a.kind), act = InboxWords.Answer, actWhy = why, actRun = () => Reply(who), click = () => Reply(who),
                        follow = ThingRefs.Of(n), order = a.opened
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: the inbox's letters", e);
            }
        }

        private static void UnderWayRows(List<WeekRow> rows)
        {
            var book = DealingBook.Instance;
            if (rows == null || book == null || !Dealings.On) return;
            try
            {
                foreach (var d in book.deals.Where(x => x.stage == DealStage.Running && x.kind != DealKind.Reply).Take(8))
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Promise ?? Icons.Trust, faction = d.people, text = LinkTextMath.Plain(Dealings.Line(d)), follow = d.Town != null ? ThingRefs.Of(d.Town) : ThingRefs.Of(d.people),
                        order = d.since
                    });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: under way", e);
            }
        }

        // ================= The Dealings tab =================

        /// <summary>The Dealings tab's section: the colony's dealings with them as a whole and with their towns, each in a line. Returns the height used.</summary>
        public static float Draw(Faction f, float x, float y, float w)
        {
            var list = Dealings.With(f);
            if (list.Count == 0) return 0f;
            float start = y;
            y += UiKit.SectionTitle(x, y, w, Dealings.W("CC_Deal_Tab_Heading"), list.Count);
            foreach (var d in list)
            {
                string line = Dealings.Line(d);
                float h = LinkText.Height(line, w - 8f, GameFont.Small);
                var color = d.stage == DealStage.Broken ? UiKit.Warn : d.Live ? Color.white : UiKit.Faint;
                LinkText.Draw(new Rect(x + 4f, y, w - 8f, h), line, color, GameFont.Small);
                y += h + 4f;
            }
            return y - start + 6f;
        }
    }
}
