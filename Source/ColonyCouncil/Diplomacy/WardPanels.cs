using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where wards and hostages show (docs/design/interface.md, "Where stage 2 and stage 3 go": Tie and Person cards, Here
    /// and away, a decision in This week). Their ward's and hostage's own card (they are the colony's lodgers, so the
    /// colonist's card: the year so far, the guardian, how they are; the acts to pick a guardian or send them home early),
    /// the card of one of the colony's away (where, since when, calling a youth home), a grown ward's card (fostered here
    /// once), the people card (what is under way with them; asking for a ward, offering a youth, each by letter), a town's
    /// card (the colony's ward or hostage there), the tie's card for a Ward or Hostage tie; rows on the Peoples view and a
    /// town's ties (DiplomacyModel); Here and away; and the one inbox: their offer of a ward, a forfeit hostage, and a ward
    /// without a guardian in Needs you (counted through Inbox.Count), every ward and hostage in Under way, in steps (never a
    /// time left).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class WardPanels
    {
        static WardPanels()
        {
            // Their ward or hostage at the colony, and the colony's own away: the colonist's card (lodgers are the colony's for the term).
            CardSections.RegisterLines(CardKind.Colonist, CardSlot.Now, 32f, Lines, null, "ward-colonist");
            CardSections.RegisterLines(CardKind.Person, CardSlot.Now, 32f, Lines, null, "ward-person");
            CardActs.Register(CardKind.Colonist, r => WardWords.P("CC_Ward_Act_Guardian"), r => Wards.GuardianWhyNot(Theirs(r)), r => Wards.ChooseGuardian(Theirs(r)),
                r => Theirs(r) != null, 36f, WardWords.P("CC_Ward_Act_Guardian_Tip"), "ward-guardian");
            CardActs.Register(CardKind.Colonist, r => WardWords.P("CC_Ward_Act_SendHome"), r => null, r => Wards.SendHome(Theirs(r)), r => Theirs(r) != null, 62f,
                WardWords.P("CC_Ward_Act_SendHome_Tip"), "ward-sendhome");
            CardActs.Register(CardKind.Colonist, r => WardWords.P("CC_Ward_Act_Recall"), r => WardsAbroad.RecallWhyNot(Ours(r)), r => WardsAbroad.Recall(Ours(r)),
                r => Ours(r) != null, 46f, WardWords.P("CC_Ward_Act_Recall_Tip"), "ward-recall");
            CardActs.Register(CardKind.Colonist, r => WardWords.P("CC_Ward_Act_Answer"), r => null, r => ForfeitLetter(Pawn(r))?.OpenLetter(),
                r => ForfeitLetter(Pawn(r)) != null, 10f, WardWords.P("CC_Ward_Act_Answer_Tip"), "hostage-answer");
            // The people card.
            CardSections.RegisterLines(CardKind.People, CardSlot.Now, 44f, r => PeopleLines(ThingRefs.Faction(r)), null, "ward-people");
            CardActs.Register(CardKind.People, r => WardWords.P("CC_Ward_Act_Ask"), r => AskAct(ThingRefs.Faction(r)).why, r => AskAct(ThingRefs.Faction(r)).run?.Invoke(),
                r => ThingRefs.Faction(r) is Faction f && !Diplomacy.Hostile(f) && Wards.On, 40f, WardWords.P("CC_Ward_Act_Ask_Tip"), "ward-ask");
            CardActs.Register(CardKind.People, r => WardWords.P("CC_Ward_Act_Send"), r => SendAct(ThingRefs.Faction(r)).why, r => SendAct(ThingRefs.Faction(r)).run?.Invoke(),
                r => ThingRefs.Faction(r) is Faction f && !Diplomacy.Hostile(f) && Wards.On, 42f, WardWords.P("CC_Ward_Act_Send_Tip"), "ward-send");
            // A town of theirs: the colony's ward or hostage there.
            CardSections.TownLines(CardSlot.Now, 42f, TownLines, null, "ward-town");
            // A Ward or Hostage tie on its own card.
            CardSections.RegisterLines(CardKind.Tie, CardSlot.Now, 7f, TieLines, null, "ward-tie");
            // The one inbox.
            Inbox.Register("ward", InboxSection.Needs, 35f, NeedRows, NeedCount);
            Inbox.Register("ward-under-way", InboxSection.UnderWay, 55f, UnderWayRows);
        }

        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static Pawn Pawn(ThingRef r) => ThingRefs.Pawn(r);

        private static WardStay Theirs(ThingRef r)
        {
            var w = Wards.Of(Pawn(r));
            return w != null && w.kind == WardKind.Theirs && w.stage == WardStage.Here ? w : null;
        }

        private static WardStay Ours(ThingRef r)
        {
            var w = Wards.Of(Pawn(r));
            return w != null && w.kind == WardKind.Ours ? w : null;
        }

        private static string Link(Pawn p, string kept) => p != null ? ThingRefs.Link(p.LabelShort, ThingRefs.Of(p)) : kept ?? "?";

        private static string LinkPeople(Faction f) => ThingRefs.Link(WardWords.People(f), ThingRefs.Of(f));

        // ================= In words =================

        /// <summary>Their ward or hostage in a sentence: coming, here (how they are), forfeit, going home (never a time left).</summary>
        public static string TheirLine(WardStay w, Map home)
        {
            string name = Link(w.pawn, w.name);
            string people = LinkPeople(w.people);
            string what = WardWords.What(w, home);
            if (w.Hostage)
                return WardWords.P("CC_Hostage_Row_" + w.stage, name.Named("HOSTAGE"), people.Named("PEOPLE"), what.Named("WHAT"));
            if (w.stage == WardStage.Here)
                return WardWords.P("CC_Ward_Row_Here", name.Named("WARD"), people.Named("PEOPLE"), what.Named("WHAT"), WardWords.Mood(Wards.Mood(w)).Named("MOOD"),
                    WardWords.Skill(w.skill).Named("SKILL"));
            return WardWords.P("CC_Ward_Row_" + (w.stage == WardStage.Coming ? "Coming" : "Going"), name.Named("WARD"), people.Named("PEOPLE"), what.Named("WHAT"));
        }

        public static string Line(WardStay w, Map home) => w.Theirs ? TheirLine(w, home) : WardsAbroad.Line(w, home);

        private static string Since(WardStay w) => w.since >= 0 ? WardWords.P("CC_Ward_Since", FactionMemory.AgoText(w.since).Named("AGO")) : null;

        // ================= Rows =================

        /// <summary>What is under way with this people at a home (or any): wards and hostages either way.</summary>
        public static List<DipRow> Rows(Faction f, Map home)
        {
            var list = new List<DipRow>();
            if (f == null) return list;
            try
            {
                foreach (var (h, w) in Wards.All())
                    if (w.people == f && (home == null || h.Map == home))
                        list.Add(new DipRow
                        {
                            text = Line(w, h.Map), note = Since(w), color = w.Theirs ? UiKit.Cool : UiKit.Warm,
                            link = w.Theirs || w.stage != WardStage.Away ? (object)w.pawn : (object)WardsAbroad.TownOf(w) ?? w.pawn, act = RowAct(w)
                        });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: the people's rows", e);
            }
            return list;
        }

        private static DipAct RowAct(WardStay w)
        {
            var letter = ForfeitLetter(w.pawn);
            if (letter != null) return new DipAct { label = InboxWords.Decide, tip = WardWords.P("CC_Ward_Act_Answer_Tip"), run = () => letter.OpenLetter() };
            if (w.kind == WardKind.Theirs && w.stage == WardStage.Here && w.guardian == null && !Wards.AtGameLessons(w.pawn))
                return new DipAct { label = WardWords.P("CC_Ward_Act_GuardianShort"), why = Wards.GuardianWhyNot(w), tip = WardWords.P("CC_Ward_Act_Guardian_Tip"), run = () => Wards.ChooseGuardian(w) };
            return null;
        }

        /// <summary>Here and away: the colony's own away as ward or hostage, or held by them.</summary>
        public static IEnumerable<DipRow> AwayRows()
        {
            foreach (var (h, w) in Wards.All())
                if (!w.Theirs && w.stage != WardStage.Coming)
                    yield return new DipRow { text = WardsAbroad.Line(w, h.Map), note = Since(w), color = w.stage == WardStage.Held ? UiKit.Warn : UiKit.Info, link = (object)WardsAbroad.TownOf(w) ?? w.pawn };
        }

        /// <summary>A town's ties: the colony's ward or hostage there.</summary>
        public static IEnumerable<DipRow> TownRows(Settlement s)
        {
            if (s == null) yield break;
            foreach (var (h, w) in Wards.All())
                if (!w.Theirs && w.town == s.ID && WardsAbroad.There(w))
                    yield return new DipRow { text = WardsAbroad.Line(w, h.Map), note = Since(w), color = UiKit.Warm, link = w.pawn };
        }

        // ================= The cards =================

        private static List<string> Lines(ThingRef r)
        {
            var p = Pawn(r);
            if (p == null) return null;
            var list = new List<string>();
            try
            {
                var w = Wards.Of(p, out var h);
                var map = h?.Map;
                if (w != null)
                {
                    list.Add(Line(w, map));
                    string since = Since(w);
                    if (since != null) list.Add(since);
                    if (w.kind == WardKind.Theirs && w.stage == WardStage.Here) TheirWardLines(w, list);
                    if (w.kind == WardKind.TheirHostage && w.stage == WardStage.Here)
                    {
                        var g = WorldLedger.Get(w.guarantees);
                        if (g != null) list.Add(WardWords.P(g.kind == Tie.Kind.Pact ? "CC_Hostage_Card_ForPact" : "CC_Hostage_Card_ForTruce", LinkPeople(w.people).Named("PEOPLE")));
                    }
                    if (w.stage == WardStage.Forfeit) list.Add(WardWords.P("CC_Hostage_Card_Forfeit", LinkPeople(w.people).Named("PEOPLE")));
                    if (w.kind == WardKind.Ours && WardsAbroad.There(w) && w.letters > 0) list.Add(WardWords.P("CC_Ward_Card_Letters", w.letters.Named("COUNT")));
                    if (w.page) list.Add(WardWords.P("CC_Ward_Card_Page"));
                }
                var guarding = Wards.GuardedBy(p);
                if (guarding != null) list.Add(WardWords.P("CC_Ward_Card_Guarding", Link(guarding.pawn, guarding.name).Named("WARD"), WardWords.Skill(guarding.skill).Named("SKILL")));
                var fostered = Wards.Fostered(p);
                if (fostered != null && w == null)
                    list.Add(WardWords.P("CC_Ward_Card_Fostered", WardWords.Result(Wards.ResultOf(fostered)).Named("RESULT")));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: the card", e);
            }
            return list;
        }

        /// <summary>Their ward's year so far: the guardian, the lessons, what they have learned, how they are fed and kept.</summary>
        private static void TheirWardLines(WardStay w, List<string> list)
        {
            var p = w.pawn;
            var skill = Wards.SkillOf(w);
            if (Wards.AtGameLessons(p)) list.Add(WardWords.P("CC_Ward_Card_GameLessons", WardWords.Skill(w.skill).Named("SKILL")));
            else if (w.guardian != null) list.Add(WardWords.P("CC_Ward_Card_Guardian", Link(w.guardian, null).Named("GUARDIAN"), WardWords.Skill(w.skill).Named("SKILL")));
            else list.Add(WardWords.P("CC_Ward_Card_NoGuardian", WardWords.Skill(w.skill).Named("SKILL")));
            if (w.lessons > 0) list.Add(WardWords.P("CC_Ward_Card_Lessons", w.lessons.Named("COUNT"), w.days.Named("DAYS")));
            if (skill != null && w.xpAtStart >= 0f)
            {
                float xp = (p.skills?.GetSkill(skill)?.XpTotalEarned ?? 0f) - w.xpAtStart;
                if (xp > 0f) list.Add(WardWords.P("CC_Ward_Card_Level", skill.label.Named("SKILL"), (p.skills?.GetSkill(skill)?.Level ?? 0).Named("LEVEL")));
            }
            var bed = p.ownership?.OwnedBed;
            list.Add(bed != null ? WardWords.P("CC_Ward_Card_Bed", bed.LabelCap.Named("BED")) : WardWords.P("CC_Ward_Card_NoBed", p.Named("WARD")));
            if (w.hungryDays > 0 && !WardMath.Fed(w.hungryDays, w.days)) list.Add(WardWords.P("CC_Ward_Card_Hungry", w.hungryDays.Named("COUNT")));
            if (w.hurt > 0) list.Add(WardWords.P("CC_Ward_Card_Hurt", w.hurt.Named("COUNT")));
        }

        private static List<string> PeopleLines(Faction f)
        {
            if (f == null) return null;
            return Rows(f, null).Select(x => x.text).ToList();
        }

        private static List<string> TownLines(Settlement s) => TownRows(s).Select(x => x.text).ToList();

        private static List<string> TieLines(ThingRef r)
        {
            var t = ThingRefs.Tie(r);
            if (t == null || t.IsView || t.kind != Tie.Kind.Ward && t.kind != Tie.Kind.Hostage) return null;
            if (t.origin != WardMath.WardOrigin && t.origin != WardMath.HostageOrigin) return null;
            string from = DipWords.NameOf(t.from), to = DipWords.NameOf(t.to);
            string about = t.terms.about.IsNone ? "?" : DipWords.NameOf(t.terms.about);
            var list = new List<string>();
            if (t.kind == Tie.Kind.Ward)
                list.Add(WardWords.P(t.from.kind == RefKind.People ? "CC_Ward_Tie_Theirs" : "CC_Ward_Tie_Ours", about.Named("WARD"), from.Named("FROM"), to.Named("TO"),
                    WardWords.Skill(t.terms.key).Named("SKILL")));
            else
            {
                var g = WorldLedger.Get(t.terms.count);
                string what = g?.kind == Tie.Kind.Pact ? WardWords.P("CC_Hostage_ForPact") : WardWords.P("CC_Hostage_ForTruce");
                list.Add(WardWords.P("CC_Hostage_Tie", about.Named("HOSTAGE"), from.Named("FROM"), to.Named("TO"), what.Named("WHAT")));
            }
            if (!t.Live && t.kind == Tie.Kind.Ward && t.from.kind == RefKind.People && t.state == Tie.State.Kept)
                list.Add(WardWords.P("CC_Ward_Tie_Result", WardWords.Result((WardResult)UnityEngine.Mathf.Clamp((int)t.terms.number, 0, 3)).Named("RESULT")));
            list.Add(DipWords.Since(t.since));
            return list;
        }

        // ================= The acts =================

        /// <summary>Asking them for a ward: a letter or a call to their head.</summary>
        public static DipAct AskAct(Faction f)
        {
            var home = Home;
            var a = new DipAct { label = WardWords.P("CC_Ward_Act_Ask"), tip = WardWords.P("CC_Ward_Act_Ask_Tip"), why = Wards.AskWhyNot(f, home) };
            Letter(a, f, home, WardMath.AskTopic);
            return a;
        }

        /// <summary>Offering a youth of the colony's to them (to the Empire, a page): a letter or a call to their head.</summary>
        public static DipAct SendAct(Faction f)
        {
            var home = Home;
            var a = new DipAct
            {
                label = WardWords.P(Ages.IsEmpire(f) ? "CC_Ward_Act_SendPage" : "CC_Ward_Act_Send"), tip = WardWords.P("CC_Ward_Act_Send_Tip"), why = WardsAbroad.SendWhyNot(f, home)
            };
            Letter(a, f, home, WardMath.SendTopic);
            return a;
        }

        private static void Letter(DipAct a, Faction f, Map home, string topic)
        {
            var head = FavourCalls.HeadOf(f, home);
            if (a.why == null && head == null) a.why = DipWords.P("CC_Dip_Why_NoNotable", WardWords.People(f).Named("PEOPLE"));
            if (a.why == null && !NeighbourLetters.On) a.why = "CC_Letter_Why_Off".Translate().Resolve();
            if (a.why == null) a.why = NeighbourLetters.WhyNot(head, LetterPurpose.Ward);
            if (a.why != null) return;
            var n = head;
            a.run = () => Find.WindowStack.Add(new Dialog_NeighbourLetter(n, LetterPurpose.Ward, topic));
        }

        /// <summary>The people card's acts (DiplomacyModel), each greyed with why not.</summary>
        public static IEnumerable<DipAct> Acts(Faction f)
        {
            yield return AskAct(f);
            yield return SendAct(f);
        }

        // ================= The inbox =================

        private static ChoiceLetter_HostageForfeit ForfeitLetter(Pawn p)
        {
            var w = Wards.Of(p);
            if (w == null || w.stage != WardStage.Forfeit || Current.Game == null) return null;
            return Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_HostageForfeit>().FirstOrDefault(l => l.ward == w.id && l.Waiting);
        }

        /// <summary>Letters about wards and hostages waiting for the colony's answer: their offer of a ward, a forfeit hostage.</summary>
        private static IEnumerable<Letter> Waiting()
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_WardOffer o && o.Waiting || l is ChoiceLetter_HostageForfeit h && h.Waiting)
                    yield return l;
        }

        /// <summary>A ward of theirs with no guardian (and not at the game's own lessons): the colony picks one.</summary>
        private static IEnumerable<(DiplomacyHome h, WardStay w)> NoGuardian() =>
            Wards.All().Where(x => x.ward.kind == WardKind.Theirs && x.ward.stage == WardStage.Here && x.ward.guardian == null && !Wards.AtGameLessons(x.ward.pawn));

        private static int NeedCount()
        {
            if (Current.Game == null) return 0;
            return Waiting().Count() + NoGuardian().Count();
        }

        /// <summary>Needs you: their offer of a ward, a forfeit hostage, a ward without a guardian.</summary>
        private static void NeedRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                foreach (var l in Waiting())
                {
                    var letter = l;
                    System.Action read = () => letter.OpenLetter();
                    string text;
                    Map map = null;
                    ThingRef follow;
                    if (l is ChoiceLetter_HostageForfeit hf)
                    {
                        var w = Wards.Get(hf.ward, out var h);
                        map = h?.Map;
                        text = WardWords.P("CC_Hostage_Week_Forfeit", Link(w?.pawn, w?.name).Named("HOSTAGE"), LinkPeople(w?.people).Named("PEOPLE"));
                        follow = w?.pawn != null ? ThingRefs.Of(w.pawn) : ThingRefs.Of(l.relatedFaction);
                    }
                    else
                    {
                        text = WardWords.P("CC_Ward_Week_Offer", LinkPeople(l.relatedFaction).Named("PEOPLE"));
                        follow = ThingRefs.Of(l.relatedFaction);
                    }
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Visitor,
                        color = l is ChoiceLetter_HostageForfeit ? UiKit.Soon : UiKit.Gold,
                        faction = l.relatedFaction,
                        text = map != null ? WeekModel.AtHome(map, text) : text,
                        sub = WardWords.P("CC_Ward_WeekNote"),
                        tip = l.Label.ToString(),
                        click = read,
                        act = l is ChoiceLetter_HostageForfeit ? InboxWords.Decide : InboxWords.Answer,
                        actRun = read,
                        follow = follow,
                        order = l.arrivalTick
                    });
                }
                foreach (var (h, w) in NoGuardian())
                {
                    var ward = w;
                    rows.Add(new WeekRow
                    {
                        icon = Icons.Visitor,
                        color = UiKit.Warm,
                        faction = w.people,
                        text = WeekModel.AtHome(h.Map, WardWords.P("CC_Ward_Week_Guardian", Link(w.pawn, w.name).Named("WARD"), WardWords.Skill(w.skill).Named("SKILL"))),
                        sub = WardWords.P("CC_Ward_Week_GuardianNote"),
                        click = () => DiplomacyLinks.Open(ward.pawn),
                        act = WardWords.P("CC_Ward_Act_GuardianShort"),
                        actWhy = Wards.GuardianWhyNot(ward),
                        actTip = WardWords.P("CC_Ward_Act_Guardian_Tip"),
                        actRun = () => Wards.ChooseGuardian(ward),
                        follow = ThingRefs.Of(w.pawn),
                        order = w.since
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: this week's asks", e);
            }
        }

        private static string[] theirSteps, oursSteps, hostageSteps;

        private static string[] Steps(params string[] keys) => keys.Select(k => WardWords.P(k)).ToArray();

        /// <summary>Under way: every ward and hostage, either way, in steps.</summary>
        private static void UnderWayRows(List<WeekRow> rows)
        {
            if (rows == null || Current.Game == null) return;
            try
            {
                foreach (var (h, w) in Wards.All())
                {
                    var ward = w;
                    string[] steps;
                    int step;
                    if (w.Hostage)
                    {
                        steps = hostageSteps ??= Steps("CC_Hostage_Step_Given", "CC_Hostage_Step_Held", "CC_Hostage_Step_Home");
                        step = w.stage == WardStage.Coming ? 0 : w.stage == WardStage.Going ? 2 : 1;
                    }
                    else if (w.Theirs)
                    {
                        steps = theirSteps ??= Steps("CC_Ward_Step_Agreed", "CC_Ward_Step_Here", "CC_Ward_Step_Home");
                        step = w.stage == WardStage.Coming ? 0 : w.stage == WardStage.Here ? 1 : 2;
                    }
                    else
                    {
                        steps = oursSteps ??= Steps("CC_Ward_Step_SettingOut", "CC_Ward_Step_Road", "CC_Ward_Step_There", "CC_Ward_Step_HomeRoad");
                        step = w.stage == WardStage.Coming ? 0 : w.stage == WardStage.Away ? (w.since < 0 ? 1 : 2) : 3;
                    }
                    rows.Add(new WeekRow
                    {
                        icon = w.Theirs ? Icons.Visitor : Icons.Away ?? Icons.Visitor,
                        color = w.stage == WardStage.Forfeit || w.stage == WardStage.Held ? UiKit.Soon : w.Theirs ? UiKit.Cool : UiKit.Warm,
                        faction = w.people,
                        text = WeekModel.AtHome(h.Map, Line(w, h.Map)),
                        steps = steps,
                        step = step,
                        click = () => DiplomacyLinks.Open((object)ward.pawn ?? ward.people),
                        follow = w.pawn != null ? ThingRefs.Of(w.pawn) : ThingRefs.Of(w.people),
                        order = w.agreed
                    });
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: this week's under way", e);
            }
        }
    }
}
