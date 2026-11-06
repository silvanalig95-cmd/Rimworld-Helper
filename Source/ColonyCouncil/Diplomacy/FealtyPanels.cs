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
    /// Where fealty and vassals show (docs/design/interface.md, "Where stage 2 and stage 3 go"): the oath as a row on the
    /// liege's people card in Region → Peoples, the tie's own card with its terms and what stands (tribute owed, a failed
    /// season), the people card's acts (swear fealty, renounce it, send what is owed), the Stance lens's line to the liege
    /// (UI/Region/LensModel.cs, the ledger's own tie); vassals on their place card (as the colony's vassal: the tribute
    /// level, their head's regard and what moved it) with its acts (set the tribute, call their levies), the Ours lens's
    /// marks and lines; and the one inbox: their offer, levies called, tribute owed and a vassal's asks in Needs you, the
    /// oath awaited, their band, the colony's levies and a vassal's levies in Under way, the tribute expected in Coming.
    /// Since the review's decision 14 (6 November 2026) they show as pacts of their own kind: "a pact of fealty" with a
    /// liege and "a pact of protection" with a vassal, among the pacts and truces on a people's card and in Needs you, the
    /// tie's card named so, and their switch under the pacts' in the settings. The mechanics are as they were.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FealtyPanels
    {
        static FealtyPanels()
        {
            CardSections.RegisterLines(CardKind.Tie, CardSlot.Now, 6f, TieLines, null, "fealty-terms");
            CardActs.Register(CardKind.Tie, r => FealtyWords.P("CC_Fealty_Act_Pay"), r => Fealty.PayWhyNot(ThingRefs.Tie(r)), r => Fealty.PayOwed(ThingRefs.Tie(r)),
                r => IsLiege(ThingRefs.Tie(r)) && Fealty.Owed(ThingRefs.Tie(r)), 18f, FealtyWords.P("CC_Fealty_Act_Pay_Tip"), "fealty-pay");
            CardActs.Register(CardKind.Tie, r => FealtyWords.P("CC_Fealty_Act_Renounce"), r => Fealty.RenounceWhyNot(ThingRefs.Tie(r)), r => FealtyObjection.AskRenounce(ThingRefs.Tie(r)),
                r => IsLiege(ThingRefs.Tie(r)), 62f, FealtyWords.P("CC_Fealty_Act_Renounce_Tip"), "fealty-renounce");
            CardActs.Register(CardKind.People, r => FealtyWords.P("CC_Fealty_Act_Swear"), r => SwearAct(ThingRefs.Faction(r)).why, r => SwearAct(ThingRefs.Faction(r)).run?.Invoke(),
                r => ThingRefs.Faction(r) is Faction f && !Diplomacy.Hostile(f) && !Ages.IsEmpire(f) && Fealty.On && Fealty.Liege(Home) == null, 34f,
                FealtyWords.P("CC_Fealty_Act_Swear_Tip"), "fealty-swear");
            CardActs.Register(CardKind.People, r => FealtyWords.P("CC_Fealty_Act_Renounce"), r => Fealty.RenounceWhyNot(Fealty.Of(Home)), r => FealtyObjection.AskRenounce(Fealty.Of(Home)),
                r => ThingRefs.Faction(r) is Faction f && Fealty.Liege(Home) == f, 62f, FealtyWords.P("CC_Fealty_Act_Renounce_Tip"), "fealty-renounce-people");
            // A vassal's place card: as the colony's vassal, and the council's acts.
            CardSections.TownLines(CardSlot.Ties, 4f, VassalLines, null, "vassal-terms");
            CardSections.PlaceLines(CardSlot.Ties, 4f, OutpostLines, null, "vassal-outpost");
            CardActs.Register(CardKind.Place, r => LevelLabel(CardSections.TownOf(r)), r => VassalOf(r) == null ? FealtyWords.P("CC_Fealty_Why_Gone") : null,
                r => ChooseLevel(VassalOf(r)), r => VassalOf(r) != null, 26f, FealtyWords.P("CC_Vassal_Act_Level_Tip"), "vassal-level");
            CardActs.Register(CardKind.Place, r => FealtyWords.P("CC_Vassal_Act_Levies"), r => Vassals.LeviesWhyNot(VassalOf(r, out var m), m), r =>
            {
                var v = VassalOf(r, out var m);
                Vassals.CallLevies(v, m);
            }, r => VassalOf(r) != null, 22f, FealtyWords.P("CC_Vassal_Act_Levies_Tip"), "vassal-levies");
            // Right after the pacts' own (32): fealty is a pact of its own kind (decision 14).
            Inbox.Register("fealty", InboxSection.Needs, 32.5f, NeedRows, NeedCount);
            Inbox.Register("fealty-under-way", InboxSection.UnderWay, 53f, UnderWayRows);
            Inbox.Register("fealty-coming", InboxSection.Coming, 26f, ComingRows);
        }

        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static bool IsLiege(Tie t) => t != null && t.kind == Tie.Kind.Fealty && t.origin == Fealty.Origin && t.Live;

        private static VassalTerms VassalOf(ThingRef r) => VassalOf(r, out _);

        private static VassalTerms VassalOf(ThingRef r, out Map home)
        {
            home = null;
            var s = CardSections.TownOf(r);
            return s == null ? null : Vassals.At(s.ID, out home);
        }

        // ================= The people card =================

        /// <summary>The oath with this people at a home as a row, in their age's words, since when, with what stands; a titled colonist's oath to the Empire as a view.</summary>
        public static List<DipRow> Rows(Faction f, Map home)
        {
            var list = new List<DipRow>();
            if (f == null) return list;
            foreach (var t in Fealty.Live())
                if (Fealty.LiegeOf(t) == f && (home == null || Fealty.HomeOf(t) == home))
                    list.Add(LiegeRow(t));
            var awaited = Fealty.Awaited(home ?? Home);
            if (awaited == f)
                list.Add(new DipRow { text = FealtyWords.P("CC_Fealty_RowAwaitedPact", FealtyWords.Form(home, f).word.Named("WORD"), FealtyWords.People(f).Named("PEOPLE")), color = UiKit.Cool, link = f });
            // Their vassals of the colony's: protected towns and the hamlet sworn.
            foreach (var (m, v) in Vassals.All())
                if (v.people == f && (home == null || m == home))
                    list.Add(VassalRow(v, m));
            // The Empire's own fealty (decision 9): its titled colonists, a view only.
            if (Ages.IsEmpire(f))
                foreach (var (p, title) in Fealty.Titled(home ?? Home))
                    if (title.people == f)
                        list.Add(new DipRow { text = FealtyWords.P("CC_Fealty_RowTitled", p.Named("PAWN"), (title.label ?? "?").Named("TITLE"), FealtyWords.People(f).Named("PEOPLE")), color = UiKit.Info, link = p });
            return list;
        }

        public static DipRow LiegeRow(Tie t)
        {
            var f = Fealty.LiegeOf(t);
            var home = Fealty.HomeOf(t);
            var form = FealtyWords.Form(home, f);
            var row = new DipRow
            {
                text = FealtyWords.P("CC_Fealty_RowPact", form.word.Named("WORD"), FealtyWords.People(f).Named("PEOPLE"), form.rite.Named("RITE")), note = State(t), color = UiKit.Gold, link = t,
                tip = FealtyWords.Terms(f, home)
            };
            if (Fealty.Owed(t))
            {
                var a = new DipAct { label = FealtyWords.P("CC_Fealty_Act_PayShort"), tip = FealtyWords.P("CC_Fealty_Act_Pay_Tip"), why = Fealty.PayWhyNot(t) };
                if (a.why == null) a.run = () => Fealty.PayOwed(t);
                row.act = a;
            }
            return row;
        }

        private static DipRow VassalRow(VassalTerms v, Map home)
        {
            var form = FealtyWords.ProtectionForm(home, v.people);
            int regard = Vassals.Regard(v.town);
            return new DipRow
            {
                text = FealtyWords.P(v.kind == VassalKind.Hamlet ? "CC_Vassal_RowPactHamlet" : "CC_Vassal_RowPact", Vassals.NameOf(v).Named("TOWN"), form.word.Named("WORD")),
                note = FealtyWords.P("CC_Vassal_RowNote", FealtyWords.Level(v.level).Named("LEVEL"), NotableBook.RegardWords(regard).Named("REGARD")), color = UiKit.Gold,
                link = Vassals.TownOf(v)
            };
        }

        /// <summary>Where the oath stands in words: since when, and what is owed, a call under way, a season they failed.</summary>
        public static string State(Tie t)
        {
            var f = Fealty.LiegeOf(t);
            string s = FealtyWords.Since(t.since);
            if (Fealty.Owed(t)) return s + "; " + FealtyWords.P("CC_Fealty_State_Owed", t.terms.count.Named("OWED"));
            if (PactMath.ParseBand(t.terms.goods, out _)) return s + "; " + FealtyWords.P("CC_Fealty_State_Band", FealtyWords.People(f).Named("PEOPLE"));
            if (PactMath.ParseArms(t.terms.goods, out int town, out _))
                return s + "; " + FealtyWords.P("CC_Fealty_State_Levies", (NotableBook.TownOf(town)?.Name ?? "?").Named("TOWN"));
            if (Fealty.FailedSeason(t)) return s + "; " + FealtyWords.P("CC_Fealty_State_Failed", FealtyWords.People(f).Named("PEOPLE"));
            return s;
        }

        /// <summary>The acts that fit a people on the Peoples view: swear fealty, or renounce it, or send what is owed, each greyed with why not.</summary>
        public static IEnumerable<DipAct> Acts(Faction f, Map home)
        {
            if (f == null || !Fealty.On || Diplomacy.Hostile(f) || Ages.IsEmpire(f)) yield break;
            var t = Fealty.Of(home);
            if (t != null && Fealty.LiegeOf(t) == f)
            {
                var r = new DipAct { label = FealtyWords.P("CC_Fealty_Act_Renounce"), tip = FealtyWords.P("CC_Fealty_Act_Renounce_Tip"), why = Fealty.RenounceWhyNot(t) };
                if (r.why == null) r.run = () => FealtyObjection.AskRenounce(t);
                yield return r;
                yield break;
            }
            if (t == null) yield return SwearAct(f);
        }

        /// <summary>Swearing fealty: their terms by their ways and the bad sides first, then the letter (or call) to their head.</summary>
        public static DipAct SwearAct(Faction f)
        {
            var home = Home;
            var a = new DipAct { label = FealtyWords.P("CC_Fealty_Act_Swear"), tip = FealtyWords.P("CC_Fealty_Act_Swear_Tip"), why = Fealty.SwearWhyNot(f, home) };
            var head = FavourCalls.HeadOf(f, home);
            if (a.why == null && head == null) a.why = DipWords.P("CC_Dip_Why_NoNotable", FealtyWords.People(f).Named("PEOPLE"));
            if (a.why == null && !NeighbourLetters.On) a.why = "CC_Letter_Why_Off".Translate().Resolve();
            if (a.why == null) a.why = NeighbourLetters.WhyNot(head, LetterPurpose.Fealty);
            if (a.why == null)
            {
                var n = head;
                a.run = () =>
                {
                    string text = FealtyWords.P("CC_Fealty_Confirm", FealtyWords.People(f).Named("PEOPLE"), FealtyWords.Form(home, f).word.Named("WORD")) + "\n\n" + FealtyWords.Terms(f, home)
                                  + "\n\n" + FealtyWords.Risks(f, home);
                    var objector = FealtyObjection.Objector(f, home, out var why);
                    if (objector != null)
                        text += "\n\n" + FealtyWords.P("CC_Fealty_Confirm_Objector_" + why, objector.Named("PAWN"), FealtyWords.People(f).Named("PEOPLE"));
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, () => Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Fealty, FealtyLetters.SwearPrefix + f.loadID))));
                };
            }
            return a;
        }

        // ================= The tie's card =================

        private static List<string> TieLines(ThingRef r)
        {
            var t = ThingRefs.Tie(r);
            if (t == null || t.kind != Tie.Kind.Fealty) return null;
            var list = new List<string>();
            try
            {
                if (t.origin == Fealty.Origin)
                {
                    var f = Fealty.LiegeOf(t);
                    var home = Fealty.HomeOf(t);
                    list.Add(LiegeRow(t).text);
                    list.Add(FealtyWords.Terms(f, home));
                    if (t.Live)
                    {
                        list.Add(State(t));
                        if (Fealty.Owed(t)) list.Add(FealtyWords.P("CC_Fealty_Card_Owed", t.terms.count.Named("OWED"), FealtyWords.People(f).Named("PEOPLE")));
                        list.Add(FealtyWords.P(Fealty.FailedSeason(t) ? "CC_Fealty_Card_Clean" : "CC_Fealty_Card_Cost", FealtyWords.People(f).Named("PEOPLE"),
                            FealtyMath.RenounceGoodwill.Named("GOODWILL")));
                    }
                }
                else if (t.origin == FealtyMath.VassalOrigin)
                {
                    var v = Vassals.At(t.from.kind == RefKind.Town ? t.from.id : -1, out var home);
                    if (v != null)
                    {
                        list.Add(VassalRow(v, home).text);
                        list.AddRange(VassalLines(Vassals.TownOf(v)) ?? new List<string>());
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fealty: the tie's card", e);
            }
            return list;
        }

        // ================= The place card =================

        /// <summary>A vassal of the colony's own, on its town's card: what it is to the colony, the tribute, its head's regard and what moved it lately.</summary>
        private static List<string> VassalLines(Settlement s)
        {
            if (s == null) return null;
            var v = Vassals.At(s.ID, out var home);
            if (v == null) return null;
            var list = new List<string>();
            var form = FealtyWords.ProtectionForm(home, v.people);
            list.Add(FealtyWords.P(v.kind == VassalKind.Hamlet ? "CC_Vassal_Card_Hamlet" : "CC_Vassal_Card", s.Name.Named("TOWN"), form.word.Named("WORD"), Homes.Name(home).Named("COLONY")));
            list.Add(FealtyWords.P("CC_Vassal_Card_Level", FealtyWords.Level(v.level).Named("LEVEL")));
            HeadLines(v.town, list);
            if (v.fell > 0) list.Add(FealtyWords.P("CC_Vassal_Card_Fell", v.fell.Named("COUNT")));
            return list;
        }

        /// <summary>One of Faction Territories' vassal outposts: the mod's own, with the head Rimfolk gives it, their regard and what moved it.</summary>
        private static List<string> OutpostLines(PlanetTile tile, WorldObject wo)
        {
            if (wo == null) return null;
            var o = Vassals.Outpost(wo.ID);
            if (o == null) return null;
            var list = new List<string> { FealtyWords.P("CC_Vassal_Card_Outpost", (o.name ?? "?").Named("TOWN"), FealtyWords.People(o.people).Named("PEOPLE")) };
            if (o.people != null && Diplomacy.Hostile(o.people)) list.Add(FealtyWords.P("CC_Vassal_Card_OutpostWar", FealtyWords.People(o.people).Named("PEOPLE")));
            else HeadLines(o.id, list);
            return list;
        }

        private static void HeadLines(int town, List<string> list)
        {
            var head = Vassals.HeadOf(town);
            if (head == null) return;
            int regard = NotableBook.Opinion(head);
            list.Add(FealtyWords.P("CC_Vassal_Card_Head", ThingRefs.Link(NotableBook.Called(head), ThingRefs.Of(head.pawn)).Named("HEAD"), NotableBook.RegardWords(regard).Named("REGARD")));
            foreach (var note in head.notes.Where(x => x != null && x.shift != 0f).OrderByDescending(x => x.tick).Take(2))
                list.Add(FealtyWords.P("CC_Vassal_Card_Moved", note.text.Named("WHY"), FactionMemory.AgoText(note.tick).Named("AGO")));
        }

        private static string LevelLabel(Settlement s)
        {
            var v = s != null ? Vassals.At(s.ID, out _) : null;
            return FealtyWords.P("CC_Vassal_Act_Level", FealtyWords.Level(v?.level ?? TributeLevel.Usual).Named("LEVEL"));
        }

        private static void ChooseLevel(VassalTerms v)
        {
            if (v == null) return;
            var opts = new List<FloatMenuOption>();
            foreach (TributeLevel l in Enum.GetValues(typeof(TributeLevel)))
            {
                var level = l;
                string label = FealtyWords.P("CC_Vassal_LevelOpt_" + l, FealtyMath.HeavyGoodwill.Named("GOODWILL"));
                opts.Add(new FloatMenuOption(level == v.level ? FealtyWords.P("CC_Vassal_LevelCurrent", label.Named("LABEL")) : label, () => Vassals.SetLevel(v, level)));
            }
            Find.WindowStack.Add(new FloatMenu(opts, FealtyWords.P("CC_Vassal_LevelMenu", Vassals.NameOf(v).Named("TOWN"))));
        }

        // ================= The inbox =================

        /// <summary>Their offer of protection, a call on the colony's levies, a vassal's ask: letters waiting for the colony's answer.</summary>
        private static IEnumerable<Letter> Waiting()
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_FealtyOffer o && o.Waiting || l is ChoiceLetter_CallToArms c && c.WaitingOnLevies || l is ChoiceLetter_VassalAsk a && a.Waiting)
                    yield return l;
        }

        private static int NeedCount()
        {
            if (WorldLedger.Instance == null) return 0;
            int n = Waiting().Count();
            foreach (var t in Fealty.Live())
                if (Fealty.Owed(t))
                    n++;
            return n;
        }

        private static void NeedRows(List<WeekRow> rows)
        {
            if (rows == null || WorldLedger.Instance == null) return;
            try
            {
                foreach (var l in Waiting())
                {
                    var letter = l;
                    Action read = () => letter.OpenLetter();
                    var f = l.relatedFaction;
                    string text;
                    if (l is ChoiceLetter_VassalAsk va)
                        text = FealtyWords.P("CC_Fealty_Week_Ask_" + (VassalAsk)va.ask, va.TownName.Named("TOWN"), ThingRefs.Link(FealtyWords.People(f), ThingRefs.Of(f)).Named("PEOPLE"));
                    else
                        text = FealtyWords.P(l is ChoiceLetter_FealtyOffer ? "CC_Fealty_Week_Offer" : "CC_Fealty_Week_Levies", ThingRefs.Link(FealtyWords.People(f), ThingRefs.Of(f)).Named("PEOPLE"),
                            FealtyWords.LevyForm(Home, f).word.Named("WORD"));
                    rows.Add(new WeekRow
                    {
                        icon = l is ChoiceLetter_CallToArms ? Icons.Raid : Icons.Promise ?? Icons.Trust,
                        color = l is ChoiceLetter_CallToArms ? UiKit.Soon : UiKit.Gold,
                        faction = f,
                        text = text,
                        sub = FealtyWords.P("CC_Fealty_WeekNote"),
                        tip = l.Label.ToString(),
                        click = read,
                        act = InboxWords.Answer,
                        actRun = read,
                        follow = ThingRefs.Of(f),
                        order = l.arrivalTick
                    });
                }
                foreach (var t in Fealty.Live())
                {
                    if (!Fealty.Owed(t)) continue;
                    var tie = t;
                    var f = Fealty.LiegeOf(t);
                    string why = Fealty.PayWhyNot(t);
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Trader,
                        color = UiKit.Soon,
                        faction = f,
                        text = WeekModel.AtHome(Fealty.HomeOf(t), FealtyWords.P("CC_Fealty_Week_Owed", ThingRefs.Link(FealtyWords.People(f), ThingRefs.Of(f)).Named("PEOPLE"),
                            t.terms.count.Named("OWED"))),
                        sub = FealtyWords.P("CC_Fealty_Week_OwedNote"),
                        click = () => DiplomacyLinks.Open(tie),
                        act = FealtyWords.P("CC_Fealty_Act_PayShort"),
                        actWhy = why,
                        actRun = why == null ? () => Fealty.PayOwed(tie) : (Action)null,
                        follow = ThingRefs.Of(t),
                        order = t.since
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fealty: this week's asks", e);
            }
        }

        private static string[] oathSteps, bandSteps, leviesSteps, vassalSteps;

        private static string[] Steps(params string[] keys) => keys.Select(k => FealtyWords.P(k)).ToArray();

        /// <summary>The oath awaited, their band coming, the colony's levies on the road to their town, a vassal's levies coming: in steps, never a time left.</summary>
        private static void UnderWayRows(List<WeekRow> rows)
        {
            if (rows == null || WorldLedger.Instance == null) return;
            try
            {
                foreach (var home in Homes.All)
                {
                    var f = Fealty.Awaited(home);
                    if (f == null) continue;
                    var lord = FealtyOath.LordOf(home);
                    int step = lord == null ? 0 : (lord.LordJob as LordJob_FealtyOath)?.arrived >= 0 ? 2 : 1;
                    var ff = f;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Promise ?? Icons.Trust,
                        color = UiKit.Gold,
                        faction = f,
                        text = WeekModel.AtHome(home, FealtyWords.P("CC_Fealty_Week_Oath", ThingRefs.Link(FealtyWords.People(f), ThingRefs.Of(f)).Named("PEOPLE"),
                            FealtyWords.Form(home, f).word.Named("WORD"))),
                        steps = oathSteps ??= Steps("CC_Fealty_Step_Agreed", "CC_Fealty_Step_Road", "CC_Fealty_Step_AtGate", "CC_Fealty_Step_Sworn"),
                        step = step,
                        click = () => DiplomacyLinks.Open(ff),
                        follow = ThingRefs.Of(f),
                        order = 0
                    });
                }
                foreach (var t in Fealty.Live())
                {
                    var f = Fealty.LiegeOf(t);
                    var home = Fealty.HomeOf(t);
                    string key;
                    string[] steps;
                    int step;
                    if (PactMath.ParseBand(t.terms.goods, out _))
                    {
                        key = "CC_Fealty_Week_Band";
                        steps = bandSteps ??= Steps("CC_Fealty_Step_Raided", "CC_Pact_Step_BandRoad", "CC_Pact_Step_Here");
                        step = 1;
                    }
                    else if (PactMath.ParseArms(t.terms.goods, out int townId, out _))
                    {
                        var town = NotableBook.TownOf(townId);
                        if (town == null || CallsToArms.Answering(town) == null) continue;
                        key = "CC_Fealty_Week_LeviesRoad";
                        steps = leviesSteps ??= Steps("CC_Pact_Step_Asked", "CC_Pact_Step_SetOut", "CC_Pact_Step_There");
                        step = 1;
                    }
                    else continue;
                    var tie = t;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Raid,
                        color = UiKit.Warm,
                        faction = f,
                        text = WeekModel.AtHome(home, FealtyWords.P(key, ThingRefs.Link(FealtyWords.People(f), ThingRefs.Of(f)).Named("PEOPLE"), FealtyWords.LevyForm(home, f).word.Named("WORD"))),
                        steps = steps,
                        step = step,
                        click = () => DiplomacyLinks.Open(tie),
                        follow = ThingRefs.Of(t),
                        order = t.since
                    });
                }
                foreach (var (home, v) in Vassals.All())
                {
                    if (v.leviesDue < 0) continue;
                    var town = Vassals.TownOf(v);
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Raid,
                        color = UiKit.Warm,
                        faction = v.people,
                        text = WeekModel.AtHome(home, FealtyWords.P("CC_Vassal_Week_Levies", Vassals.NameOf(v).Named("TOWN"), FealtyWords.LevyForm(home, v.people).rite.Named("RITE"))),
                        steps = vassalSteps ??= Steps("CC_Vassal_Step_Called", "CC_Pact_Step_BandRoad", "CC_Pact_Step_Here"),
                        step = 1,
                        click = () =>
                        {
                            if (town != null) DiplomacyLinks.Open(town);
                        },
                        follow = town != null ? ThingRefs.Of(town) : ThingRef.None,
                        order = v.leviesAt
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fealty: this week's under way", e);
            }
        }

        /// <summary>The season's tribute expected soon (within three days), and whether the stores hold it: never a countdown.</summary>
        private static void ComingRows(List<WeekRow> rows)
        {
            if (rows == null || WorldLedger.Instance == null) return;
            try
            {
                float day = Find.TickManager.TicksGame / (float)FealtyMath.DayTicks;
                foreach (var t in Fealty.Live())
                {
                    if (Fealty.Owed(t) || t.terms.number - day > 3f) continue;
                    var f = Fealty.LiegeOf(t);
                    var home = Fealty.HomeOf(t);
                    int value = Fealty.TributeValue(home);
                    var tie = t;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Trader,
                        color = UiKit.Gold,
                        faction = f,
                        text = WeekModel.AtHome(home, FealtyWords.P("CC_Fealty_Week_Tribute", ThingRefs.Link(FealtyWords.People(f), ThingRefs.Of(f)).Named("PEOPLE"), value.Named("VALUE"))),
                        note = InboxWords.Date(Mathf.RoundToInt(t.terms.number * FealtyMath.DayTicks)),
                        click = () => DiplomacyLinks.Open(tie),
                        follow = ThingRefs.Of(t),
                        order = t.terms.number
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fealty: this week's tribute", e);
            }
        }
    }

    /// <summary>
    /// "Call the levies of Ashford": a threat is on a home that has a vassal of its own whose levies the council could call
    /// now (once a season). A click calls them; the explanation says what comes and what it may cost them.
    /// </summary>
    public class Alert_VassalLevies : Alert
    {
        private VassalTerms vassal;
        private Map home;
        private int lookedAt = -1;

        public Alert_VassalLevies()
        {
            defaultPriority = AlertPriority.High;
        }

        public override string GetLabel() => vassal != null ? FealtyWords.P("CC_Vassal_Alert", Vassals.NameOf(vassal).Named("TOWN")) : "";

        public override TaggedString GetExplanation() =>
            vassal == null ? "" : FealtyWords.P("CC_Vassal_AlertWhat", Vassals.NameOf(vassal).Named("TOWN"), FealtyWords.LevyForm(home, vassal.people).rite.Named("RITE"));

        public override AlertReport GetReport()
        {
            if (!Vassals.On || Current.Game == null) return false;
            int now = Find.TickManager.TicksGame;
            if (now / Sched.RareTicks != lookedAt)
            {
                lookedAt = now / Sched.RareTicks;
                vassal = null;
                home = null;
                try
                {
                    foreach (var (m, v) in Vassals.All())
                    {
                        if (m == null || !MapFacts.ThreatOn(m) || Vassals.LeviesWhyNot(v, m) != null) continue;
                        vassal = v;
                        home = m;
                        break;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("vassals: the levies alert", e);
                }
            }
            return vassal != null;
        }

        protected override void OnClick()
        {
            if (vassal != null && home != null) Vassals.CallLevies(vassal, home);
            lookedAt = -1;
        }
    }
}
