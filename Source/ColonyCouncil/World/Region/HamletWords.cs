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
    /// The words of the hamlets (Languages/English/Keyed/Rimfolk_Hamlets.xml): the hamlet's word by its people's age (a new
    /// hearth, a hamlet, a homestead, an outpost colony), the colony's protection by its own (the clan's spear, a lord's
    /// protection, a protectorate's guarantee, a charter of protection), the oath by theirs and where the colony takes it
    /// by its own (Ages.Form, the features Hamlet and Oath); the questions, letters and news of the settlers' road and the
    /// hamlet's life; the history's lines; the region map's panel and the caravan's line. Every line goes to the player
    /// through Tell, and says only what the map shows.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HamletWords
    {
        private static readonly Color Accent = new Color(0.62f, 0.80f, 0.55f);

        static HamletWords()
        {
            NeighbourPanel.Sections.Add(DrawHamlet);
        }

        private static string T(string key, params NamedArgument[] args) => TextVariants.Vary(key).Translate(args).Resolve();

        private static string PeopleName(Faction f) => f?.Name ?? "CC_RiseFall_Someone".Translate().Resolve();

        /// <summary>The hamlet's word by its people's age, with its article: "a new hearth", "a hamlet", "a homestead", "an outpost colony".</summary>
        public static string Word(Faction people) => AgesMath.RiteKey(AgeFeature.Hamlet, Ages.Of(people)).Translate().Resolve();

        /// <summary>The colony's protection by its own age: "the clan's spear", "a lord's protection", "a protectorate's guarantee", "a charter of protection".</summary>
        public static string Protection(Map home, Faction people) => Ages.Form(AgeFeature.Hamlet, home, people).word;

        /// <summary>The oath by their age ("gifts at the river") and where the colony takes it by its own ("the council fire").</summary>
        public static string OathRite(Map home, Faction people) => Ages.Form(AgeFeature.Oath, home, people).rite;

        public static string OathPlace(Map home, Faction people) => Ages.Form(AgeFeature.Oath, home, people).word;

        /// <summary>How the colony hears of something far off, by its own age: word from a traveler, a rider's news, the radio, a comms relay.</summary>
        private static string News(Map home, Faction people) => Ages.Form(AgeFeature.Founding, home, people).word;

        /// <summary>What a grown town's texts call the hamlet it was ({TALE}).</summary>
        public static string TaleOf(HamletHome h) => Word(h?.people);

        private static string Leader(HamletHome h)
        {
            var p = h?.leader;
            if (p != null && !p.Dead) return p.LabelShort;
            return h?.Living.FirstOrDefault()?.LabelShort ?? "CC_Hamlet_TheirLeader".Translate().Resolve();
        }

        private static string HamletName(HamletHome h) => !h.name.NullOrEmpty() ? h.name : "CC_Hamlet_TheirHamlet".Translate().Resolve();

        private static string Colony(Map home) => home != null ? Homes.Name(home) : "";

        private static string From(HamletHome h) => !h.fromName.NullOrEmpty() ? h.fromName : PeopleName(h.people);

        /// <summary>The common words of a hamlet's lines.</summary>
        private static List<NamedArgument> Args(Map home, HamletHome h) => new List<NamedArgument>
        {
            PeopleName(h.people).Named("PEOPLE"), HamletName(h).Named("HAMLET"), Leader(h).Named("LEADER"), Word(h.people).Named("WORD"),
            Protection(home, h.people).Named("PROTECTION"), Hamlets.Where(home, h).Named("WHERE"), Colony(home).Named("COLONY"), From(h).Named("FROM"),
            OathRite(home, h.people).Named("RITE"), OathPlace(home, h.people).Named("OATHPLACE"), News(home, h.people).CapitalizeFirst().Named("NEWS")
        };

        private static LookTargets Look(HamletHome h)
        {
            var town = Hamlets.TownOf(h);
            if (town != null) return new LookTargets(town);
            var t = Hamlets.TileOf(h);
            return t.Valid ? new LookTargets(new GlobalTargetInfo(t)) : LookTargets.Invalid;
        }

        // ---------------- The settlers' ask ----------------

        public static string AskLabel(HamletHome h) => T("CC_Hamlet_Ask_Label", PeopleName(h.people).Named("PEOPLE"));

        public static string AskText(Map home, HamletHome h)
        {
            var args = Args(home, h);
            var camp = h.way == HamletWay.Holding ? Hamlets.CampOf(h) : null;
            args.Add((camp?.HoldingName ?? "").Named("HOLDING"));
            var names = h.Living.Select(p => p.LabelShort).ToList();
            args.Add(names.Count.Named("COUNT"));
            args.Add(TextUtil.JoinAnd(names).Named("NAMES"));
            var a = args.ToArray();
            string first = h.way == HamletWay.Holding && camp != null ? T("CC_Hamlet_Ask_Holding", a)
                : h.way == HamletWay.Refugees ? T("CC_Hamlet_Ask_Refugees", a)
                : T("CC_Hamlet_Ask_Town", a);
            return first + "\n\n" + T("CC_Hamlet_Ask_How", a) + "\n\n" + T("CC_Hamlet_Ask_Who", a);
        }

        // ---------------- The road ----------------

        /// <summary>Taken under the colony's protection: where they wait, and what takes them along.</summary>
        public static void TellWaiting(Map home, HamletHome h, Camp camp)
        {
            var a = Args(home, h).ToArray();
            string key = h.way == HamletWay.Holding && camp != null ? "CC_Hamlet_Waiting_Holding" : camp != null ? "CC_Hamlet_Waiting_Camp" : "CC_Hamlet_Waiting_Road";
            var look = camp?.leader != null && camp.leader.Spawned ? new LookTargets(camp.leader) : Look(h);
            Tell.Message(NewsKind.World, Importance.Important, home, T(key, a) + " " + T("CC_Hamlet_Waiting_How", a), look, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>A caravan of the colony's takes them along.</summary>
        public static void TellSetOut(Map home, HamletHome h, Caravan c)
        {
            var args = Args(home, h);
            args.Add((c?.Name ?? "").Named("CARAVAN"));
            Tell.Message(NewsKind.World, Importance.Important, home, T("CC_Hamlet_SetOut", args.ToArray()), c != null ? new LookTargets(c) : Look(h), MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>The settlers take up arms beside the caravan in a fight on the road.</summary>
        public static void TellFight(Map home, HamletHome h, List<Pawn> stand)
        {
            Tell.Message(NewsKind.World, Importance.Important, home, T("CC_Hamlet_Fight", Args(home, h).ToArray()), new LookTargets(stand.Cast<Thing>()), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>They set off alone.</summary>
        public static void TellAlone(Map home, HamletHome h, bool waited)
        {
            string key = waited ? "CC_Hamlet_Alone_Waited" : h.arrivedAt < 0 && h.escorts.Count > 0 ? "CC_Hamlet_Alone_Turned" : "CC_Hamlet_Alone";
            Tell.Message(NewsKind.World, Importance.Notable, home, T(key, Args(home, h).ToArray()), Look(h), MessageTypeDefOf.NeutralEvent);
        }

        // ---------------- The hamlet stands ----------------

        /// <summary>The founding's letter, looking at the hamlet.</summary>
        public static void TellFounded(Map home, HamletHome h, Settlement town, bool escorted, List<Pawn> walked)
        {
            try
            {
                var args = Args(home, h);
                args.Add(TextUtil.JoinAnd(walked.Select(p => p.LabelShort).ToList()).Named("WALKED"));
                string families = RegionWords.Families(town.ID);
                args.Add(families.Named("FAMILIES"));
                var a = args.ToArray();
                var parts = new List<string> { T(escorted ? "CC_Hamlet_Founded_Escorted" : "CC_Hamlet_Founded_Alone", a) };
                if (escorted && walked.Count > 0) parts.Add(T("CC_Hamlet_Founded_Walked", a));
                parts.Add(T(families.NullOrEmpty() ? "CC_Hamlet_Founded_Life_NoFamilies" : "CC_Hamlet_Founded_Life", a));
                Tell.Letter(NewsKind.World, Importance.Important, home, T("CC_Hamlet_Founded_Label", a), string.Join("\n\n", parts), LetterDefOf.PositiveEvent, new LookTargets(town), h.people);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: the founding's letter", e);
            }
        }

        /// <summary>What its head remembers of the colony from the start (a note of the notables').</summary>
        public static string NoteFounded(Settlement town) => T("CC_Hamlet_Note_Founded", (town?.Name ?? "").Named("HAMLET"));

        // ---------------- Its life ----------------

        public static string HelpGoods(HamletNeed need)
        {
            var (what, count) = Hamlets.Help(need);
            return T("CC_Hamlet_Goods", count.Named("COUNT"), PlayCarry.Label(what, PlayCarry.DefOf(what, null)).Named("LABEL"));
        }

        public static string HelpLabel(HamletHome h, HamletNeed need) => T("CC_Hamlet_Help_Label", HamletName(h).Named("HAMLET"));

        public static string HelpText(Map home, HamletHome h, HamletNeed need)
        {
            var args = Args(home, h);
            args.Add(HelpGoods(need).Named("GOODS"));
            var a = args.ToArray();
            return T("CC_Hamlet_Help_" + need, a) + "\n\n" + T("CC_Hamlet_Help_Why", a);
        }

        /// <summary>The harvest's carter: on the way to the gate (a courier of theirs), or left at the gate.</summary>
        public static void TellHarvest(Map home, HamletHome h, int count, ThingDef def, bool carried, IntVec3 gate)
        {
            var args = Args(home, h);
            args.Add(T("CC_Hamlet_Goods", count.Named("COUNT"), (def?.label ?? "").Named("LABEL")).Named("GOODS"));
            var look = gate.IsValid ? new LookTargets(new TargetInfo(gate, home)) : LookTargets.Invalid;
            Tell.Message(NewsKind.World, Importance.Notable, home, T(carried ? "CC_Hamlet_Harvest_Coming" : "CC_Hamlet_Harvest_Left", args.ToArray()), look, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>Help refused, once this season.</summary>
        public static void TellRefused(Map home, HamletHome h) =>
            Tell.Minor(NewsKind.World, Importance.Notable, home, T("CC_Hamlet_Refused", Args(home, h).ToArray()), Look(h), MessageTypeDefOf.NegativeEvent);

        public static string OathLabel(HamletHome h) => T("CC_Hamlet_Oath_Label", HamletName(h).Named("HAMLET"));

        public static string OathText(Map home, HamletHome h)
        {
            var a = Args(home, h).ToArray();
            bool vassal = Territories.Active;
            return T("CC_Hamlet_Oath_Text", a) + "\n\n" + T(vassal ? "CC_Hamlet_Oath_Vassal" : "CC_Hamlet_Oath_Tie", a);
        }

        /// <summary>"Not yet": they will ask again.</summary>
        public static void TellNotYet(Map home, HamletHome h) =>
            Tell.Minor(NewsKind.World, Importance.Notable, home, T("CC_Hamlet_NotYet", Args(home, h).ToArray()), Look(h), MessageTypeDefOf.NeutralEvent);

        /// <summary>Sworn (the tie): the letter.</summary>
        public static void TellSworn(Map home, HamletHome h, Settlement town)
        {
            var a = Args(home, h).ToArray();
            Tell.Letter(NewsKind.World, Importance.Important, home, T("CC_Hamlet_Sworn_Label", a), T("CC_Hamlet_Sworn_Text", a), LetterDefOf.PositiveEvent,
                town != null ? new LookTargets(town) : Look(h), h.people);
        }

        public static string HostileLabel(HamletHome h) => T("CC_Hamlet_Hostile_Label", HamletName(h).Named("HAMLET"));

        public static string HostileText(Map home, HamletHome h) => T("CC_Hamlet_Hostile_Text", Args(home, h).ToArray());

        /// <summary>Who came from the hamlet to the colony, and whether the hamlet was left empty.</summary>
        public static string Joined(HamletHome h, List<Pawn> joined, bool ruin)
        {
            string who = TextUtil.JoinAnd(joined.Select(p => p.LabelShort).ToList());
            var hamlet = HamletName(h).Named("HAMLET");
            string first = joined.Count > 0 ? T("CC_Hamlet_Joined_Who", who.Named("WHO"), hamlet) : T("CC_Hamlet_Joined_Nobody", hamlet);
            return first + " " + T(ruin ? "CC_Hamlet_Joined_Empty" : "CC_Hamlet_Joined_Stayed");
        }

        // ---------------- Its end ----------------

        /// <summary>How the story ended: a letter for a loss or a turn, a line in the news for the quieter ends.</summary>
        public static void TellEnded(Map home, HamletHome h, HamletEnd why, HamletStage was, bool stood, string extra)
        {
            if (home == null || h == null) return;
            try
            {
                var args = Args(home, h);
                args.Add((extra ?? "").Named("EXTRA"));
                var a = args.ToArray();
                var look = Look(h);
                switch (why)
                {
                    case HamletEnd.Refused:
                        Tell.Minor(NewsKind.World, Importance.Notable, home, T(extra == "refused" ? "CC_Hamlet_End_Refused" : "CC_Hamlet_End_Unanswered", a), look, MessageTypeDefOf.NeutralEvent);
                        return;
                    case HamletEnd.TurnedBack:
                        Tell.Letter(NewsKind.World, Importance.Notable, home, T("CC_Hamlet_End_TurnedBack_Label", a), T("CC_Hamlet_End_TurnedBack", a), LetterDefOf.NegativeEvent, look, h.people);
                        return;
                    case HamletEnd.Lost:
                        Tell.Letter(NewsKind.World, Importance.Notable, home, T("CC_Hamlet_End_Lost_Label", a), T("CC_Hamlet_End_Lost", a), LetterDefOf.NegativeEvent, look, h.people);
                        return;
                    case HamletEnd.Broken:
                        Tell.Letter(NewsKind.World, Importance.Notable, home, T("CC_Hamlet_End_Broken_Label", a), T("CC_Hamlet_End_Broken", a), LetterDefOf.NegativeEvent, look, h.people);
                        return;
                    case HamletEnd.Joined:
                        Tell.Letter(NewsKind.World, Importance.Important, home, T("CC_Hamlet_End_Joined_Label", a), T("CC_Hamlet_End_Joined", a), LetterDefOf.NeutralEvent, LookTargets.Invalid,
                            h.people);
                        return;
                    case HamletEnd.Stayed:
                        Tell.Letter(NewsKind.World, Importance.Notable, home, T("CC_Hamlet_End_Stayed_Label", a), T("CC_Hamlet_End_Stayed", a), LetterDefOf.NegativeEvent, look, h.people);
                        return;
                    case HamletEnd.Vassal:
                        Tell.Letter(NewsKind.World, Importance.Important, home, T("CC_Hamlet_Sworn_Label", a), T("CC_Hamlet_End_Vassal", a), LetterDefOf.PositiveEvent, look, h.people);
                        return;
                    case HamletEnd.Failed:
                        Tell.Minor(NewsKind.World, Importance.Notable, home, T("CC_Hamlet_End_Failed", a), look, MessageTypeDefOf.NegativeEvent);
                        return;
                    case HamletEnd.Gone:
                        Tell.Minor(NewsKind.World, Importance.Notable, home, T(stood ? "CC_Hamlet_End_Gone" : "CC_Hamlet_End_GoneHome", a), look, MessageTypeDefOf.NeutralEvent);
                        return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: its end told", e);
            }
        }

        // ---------------- The history's lines ----------------

        private static NamedArgument[] H(HamletHome h, Map home) => new[]
        {
            PeopleName(h.people).Named("PEOPLE"), HamletName(h).Named("HAMLET"), Colony(home).Named("COLONY")
        };

        public static string HistoryFounded(HamletHome h, Map home) => T("CC_Hamlet_H_Founded", H(h, home));

        public static string HistorySworn(HamletHome h, Map home) => T("CC_Hamlet_H_Sworn", H(h, home));

        public static string HistoryEnded(HamletHome h, HamletEnd why) => T("CC_Hamlet_H_" + why, H(h, Hamlets.HomeOf(h)));

        public static string HistoryHarvest(HamletHome h, int count, ThingDef def)
        {
            var a = new List<NamedArgument>(H(h, Hamlets.HomeOf(h))) { T("CC_Hamlet_Goods", count.Named("COUNT"), (def?.label ?? "").Named("LABEL")).Named("GOODS") };
            return T("CC_Hamlet_H_Harvest", a.ToArray());
        }

        public static string HistoryHelped(HamletHome h, HamletNeed need)
        {
            var a = new List<NamedArgument>(H(h, Hamlets.HomeOf(h))) { HelpGoods(need).Named("GOODS") };
            return T("CC_Hamlet_H_Helped", a.ToArray());
        }

        public static string HistoryRefused(HamletHome h, HamletNeed need) => T("CC_Hamlet_H_Refused", H(h, Hamlets.HomeOf(h)));

        // ---------------- The panel, the page, the caravan ----------------

        /// <summary>A hamlet's lines: what and whose it is and since when, its head, its tie, what it asked lately.</summary>
        public static List<string> Lines(Map home, HamletHome h, bool withHome)
        {
            var lines = new List<string>();
            if (h == null) return lines;
            var args = Args(home, h);
            args.Add(FactionMemory.AgoText(h.founded >= 0 ? h.founded : h.askedAt).Named("AGO"));
            args.Add((home != null ? Colony(home) : "").Named("HOME"));
            var a = args.ToArray();
            switch (h.stage)
            {
                case HamletStage.Asked:
                    lines.Add(T("CC_Hamlet_Panel_Asked", a));
                    break;
                case HamletStage.Waiting:
                    lines.Add(T(Hamlets.CampOf(h) != null ? "CC_Hamlet_Panel_Waiting" : "CC_Hamlet_Panel_WaitingRoad", a));
                    break;
                case HamletStage.Escorted:
                    lines.Add(T(h.arrivedAt >= 0 ? "CC_Hamlet_Panel_There" : "CC_Hamlet_Panel_Escorted", a));
                    break;
                case HamletStage.Alone:
                    lines.Add(T("CC_Hamlet_Panel_Alone", a));
                    break;
                case HamletStage.Standing:
                case HamletStage.Sworn:
                    lines.Add(T(withHome && Homes.All.Count > 1 ? "CC_Hamlet_Panel_WhatHome" : "CC_Hamlet_Panel_What", a));
                    if (h.leader != null && !h.leader.Dead) lines.Add(T("CC_Hamlet_Panel_Head", a));
                    if (h.stage == HamletStage.Sworn) lines.Add(T("CC_Hamlet_Panel_Sworn", a));
                    if (h.grown) lines.Add(T("CC_Hamlet_Panel_Grown", a));
                    if (HamletMath.LastRefused(h.refused) is int r && r >= 0 && Find.TickManager.TicksGame - r < (long)HamletMath.SeasonDays * HamletMath.DayTicks)
                        lines.Add(T("CC_Hamlet_Panel_Refused", a));
                    break;
            }
            return lines;
        }

        /// <summary>The region map's panel for a town that is a hamlet: its own section under the town's.</summary>
        private static float DrawHamlet(Rect rect, Settlement s)
        {
            if (s == null) return 0f;
            try
            {
                var h = Hamlets.HamletAt(s.ID, out var home);
                if (h == null) return 0f;
                var lines = Lines(home, h, true);
                if (lines.Count == 0) return 0f;
                float y = rect.y;
                UiKit.LabelFit(new Rect(rect.x, y, rect.width, 22f), "CC_Hamlet_Panel_Header".Translate(), Accent);
                y += 24f;
                foreach (var l in lines) y += UiKit.Paragraph(rect.x, y, rect.width, l, Color.white, GameFont.Small) + 2f;
                return y + 2f - rect.y;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: a hamlet's panel", e);
                return 0f;
            }
        }

        /// <summary>The caravan's panel line for the settlers beside it (decision 10), or null.</summary>
        public static string CaravanLine(Caravan c)
        {
            var h = Hamlets.EscortedBy(c, out var home);
            if (h == null) return null;
            var args = Args(home, h);
            args.Add(h.Living.Count.Named("COUNT"));
            return T(h.arrivedAt >= 0 ? "CC_Hamlet_Caravan_There" : "CC_Hamlet_Caravan", args.ToArray());
        }
    }
}
