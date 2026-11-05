using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Their ward at the colony (docs/design/stage2-diplomacy.md, round b, item 7; release 2b.3; the player's decision 10;
    /// the rules in WardMath.cs), and the hour of every ward and hostage either way (the colony's own away: WardsAbroad.cs;
    /// hostages' terms: Hostages.cs). A people at peace whose head thinks 30 or more of the colony may offer a youth of a
    /// notable's house (a child where the game has children, else 14 to 19) to live at the colony for a year and learn a
    /// skill it is known for, from a guardian with 10 or more in it, once a year per people and a season apart at a home;
    /// or the colony asks by letter. Agreed, the ward walks in with one of their kin to the gate and joins the colony as the
    /// game's lodger (a quest of Rimfolk's own with the game's QuestPart_ExtraFaction, their people the home people, as the
    /// game's refugee hosting does): on the colonist bar, a bed of their own, working with the colony. The guardian teaches
    /// them an hour a day when both are free and together (a child with Biotech learns at the game's own lessons). How they
    /// are shows as homesick, settling in or happy. At the year's end their kin come to the gate for them, the ward walks out
    /// to them, and how they were kept (taught, fed, unhurt, content) moves their people: a year kept well is a favour owed
    /// (the deed "fostered"), and the grown ward, as a notable of theirs, still thinks well of the colony. A ward hurt or
    /// killed in the colony's care grieves their people, a little where the colony sheltered them, much in the open, and a
    /// death at a colonist's hand is a feud (a grudge). Kept with KeepFor.Ward from agreed to come, and as fostered after.
    /// </summary>
    public static class Wards
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The setting "Wards and hostages": off, nothing new is agreed, and those under way run their term.</summary>
        public static bool On => ColonyCouncilMod.Settings?.diplomacyWards ?? true;

        public const string QuestRoot = "Rimfolk_LodgerQuest";

        // ================= Reading =================

        /// <summary>Every ward and hostage under way, either way, with its home (a new list).</summary>
        public static List<(DiplomacyHome home, WardStay ward)> All()
        {
            var list = new List<(DiplomacyHome, WardStay)>();
            var book = DiplomacyBook.Instance;
            if (book == null) return list;
            foreach (var h in book.Live())
                foreach (var w in h.wards)
                    if (w != null)
                        list.Add((h, w));
            return list;
        }

        /// <summary>The record this pawn is the ward or hostage of, or null.</summary>
        public static WardStay Of(Pawn p, out DiplomacyHome home)
        {
            home = null;
            if (p == null) return null;
            foreach (var (h, w) in All())
                if (w.pawn == p)
                {
                    home = h;
                    return w;
                }
            return null;
        }

        public static WardStay Of(Pawn p) => Of(p, out _);

        public static WardStay Get(int id, out DiplomacyHome home)
        {
            home = null;
            foreach (var (h, w) in All())
                if (w.id == id)
                {
                    home = h;
                    return w;
                }
            return null;
        }

        /// <summary>Their ward at (or coming to) this home, or null.</summary>
        public static WardStay TheirsAt(Map home) => DiplomacyBook.Instance?.Existing(home)?.wards.FirstOrDefault(w => w != null && w.kind == WardKind.Theirs);

        /// <summary>The colony's own ward away from this home (or about to go), or null.</summary>
        public static WardStay OursFrom(Map home) => DiplomacyBook.Instance?.Existing(home)?.wards.FirstOrDefault(w => w != null && w.kind == WardKind.Ours);

        /// <summary>Wards and hostages either way with this people (a new list).</summary>
        public static List<WardStay> With(Faction f) => All().Where(x => x.ward.people == f).Select(x => x.ward).ToList();

        /// <summary>The guardian of a ward of theirs, or null.</summary>
        public static WardStay GuardedBy(Pawn guardian) => All().Select(x => x.ward).FirstOrDefault(w => w.kind == WardKind.Theirs && w.guardian == guardian && w.stage == WardStage.Here);

        /// <summary>
        /// KeepFor.Ward still holds: a ward of theirs agreed and not yet come (in the world), or one fostered at the colony and
        /// gone home, who remembers it while they live (their Regard tie toward the colony).
        /// </summary>
        public static bool Holds(Pawn p)
        {
            if (p == null || p.Dead) return false;
            var w = Of(p);
            if (w != null && w.kind == WardKind.Theirs) return true;
            return Fostered(p) != null;
        }

        /// <summary>
        /// One fostered at the colony and gone home: their Regard tie toward the colony (origin "ward", key "fostered"), the
        /// regard the grown ward keeps in its number and how the year went in its count; or null.
        /// </summary>
        public static Tie Fostered(Pawn p)
        {
            if (p == null) return null;
            var r = Refs.Of(p);
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Regard))
                if (t != null && t.Live && t.origin == WardMath.WardOrigin && t.from == r && t.terms?.key == WardMath.FosteredKey)
                    return t;
            return null;
        }

        /// <summary>How the year went for one fostered here, from their Regard tie.</summary>
        public static WardResult ResultOf(Tie fostered) => (WardResult)Mathf.Clamp(fostered?.terms?.count ?? 0, 0, 3);

        public static Lord LordOf(WardStay w, Map map)
        {
            if (w == null || map?.lordManager == null) return null;
            foreach (var l in map.lordManager.lords)
                if (l?.LordJob is LordJob_WardParty j && j.ward == w.id)
                    return l;
            return null;
        }

        public static float DaysHere(WardStay w) => w == null || w.since < 0 ? 0f : (Now - w.since) / (float)WardMath.DayTicks;

        /// <summary>How their ward is, in a word.</summary>
        public static WardMood Mood(WardStay w) => WardMath.Mood(w?.pawn?.needs?.mood?.CurLevelPercentage ?? 0.5f, DaysHere(w));

        /// <summary>A child at the game's own lessons (Biotech): taught there, not by the guardian's hour.</summary>
        public static bool AtGameLessons(Pawn p) => ModsConfig.BiotechActive && p != null && p.DevelopmentalStage.Juvenile();

        // ================= Guardians and the skill =================

        /// <summary>Those at the home who could be a ward's guardian in this skill: grown colonists of its own with 10 or more, best first.</summary>
        public static List<Pawn> Guardians(Map home, SkillDef skill, Pawn except = null)
        {
            var list = new List<Pawn>();
            if (home == null || skill == null) return list;
            foreach (var p in home.mapPawns.FreeColonistsSpawned)
            {
                if (p == null || p == except || p.Dead || p.IsQuestLodger() || p.DevelopmentalStage.Juvenile() || Of(p) != null) continue;
                var s = p.skills?.GetSkill(skill);
                if (s == null || s.TotallyDisabled || s.Level < WardMath.GuardianSkill) continue;
                list.Add(p);
            }
            return list.OrderByDescending(p => p.skills.GetSkill(skill).Level).ToList();
        }

        private static List<SkillDef> Teachable(Map home) =>
            DefDatabase<SkillDef>.AllDefsListForReading.Where(s => s != null && Guardians(home, s).Count > 0).ToList();

        /// <summary>The skills the colony could teach a ward at this home (one with a guardian for it), best known first.</summary>
        public static List<SkillDef> Skills(Map home)
        {
            var list = Teachable(home);
            return list.OrderByDescending(s => Guardians(home, s).FirstOrDefault()?.skills.GetSkill(s).Level ?? 0).ToList();
        }

        private static readonly Dictionary<int, (int at, SkillDef skill)> knownFor = new Dictionary<int, (int, SkillDef)>();

        /// <summary>The skill the colony is known for at this home (WardMath.KnownFor), or null; worked out at most once a 250-tick stretch a home.</summary>
        public static SkillDef KnownFor(Map home)
        {
            if (home == null) return null;
            int now = Now;
            if (knownFor.TryGetValue(home.uniqueID, out var was) && Fresh.Holds(was.at, now, Sched.RareTicks)) return was.skill;
            var skill = KnownForNow(home);
            knownFor[home.uniqueID] = (now, skill);
            return skill;
        }

        private static SkillDef KnownForNow(Map home)
        {
            var all = DefDatabase<SkillDef>.AllDefsListForReading;
            var best = new List<int>();
            foreach (var s in all) best.Add(Guardians(home, s).FirstOrDefault()?.skills.GetSkill(s).Level ?? 0);
            int i = WardMath.KnownFor(best);
            return i >= 0 ? all[i] : null;
        }

        // ================= Why it can be asked =================

        /// <summary>Why the colony can't ask this people for a ward at this home now, or null.</summary>
        public static string AskWhyNot(Faction f, Map home)
        {
            if (!On) return WardWords.P("CC_Ward_Why_Off");
            if (f == null || !Diplomacy.Deals(f)) return DipWords.P("CC_Dip_Why_Gone");
            if (Diplomacy.Hostile(f)) return WardWords.P("CC_Ward_Why_War", WardWords.People(f).Named("PEOPLE"));
            if (home == null) return DipWords.P("CC_Dip_Why_Gone");
            var h = DiplomacyBook.Instance?.Existing(home);
            if (h != null && h.wards.Any(w => w != null && w.kind == WardKind.Theirs)) return WardWords.P("CC_Ward_Why_OneHere");
            if (h != null && h.wards.Count >= WardMath.MaxPerHome) return WardWords.P("CC_Ward_Why_Full");
            if (KnownFor(home) == null) return WardWords.P("CC_Ward_Why_NoGuardian", WardMath.GuardianSkill.Named("SKILL"));
            if (Kin(f, home, out _) == null) return WardWords.P("CC_Ward_Why_NoKin", WardWords.People(f).Named("PEOPLE"));
            return null;
        }

        /// <summary>The notable of theirs whose house the ward is from: their head, heir or envoy (of the town nearest the home first), or null.</summary>
        public static Notable Kin(Faction f, Map home, out Settlement town)
        {
            town = home != null ? WorldSituationWorker.NearestTown(f, home) : null;
            var book = NotableBook.Instance;
            if (f == null || book == null) return null;
            int near = town?.ID ?? -1;
            return book.notables.Where(n => n != null && n.faction == f && n.Alive && (n.role == NotableRole.Head || n.role == NotableRole.Heir || n.role == NotableRole.Envoy))
                .OrderBy(n => n.town == near ? 0 : 1).ThenBy(n => n.role == NotableRole.Head ? 0 : n.role == NotableRole.Heir ? 1 : 2).FirstOrDefault();
        }

        // ================= Their offer =================

        /// <summary>When this people last offered or sent a ward (the history's dip:ward with them as the ones who did), or -1.</summary>
        private static int LastOf(Faction f)
        {
            var r = Refs.Of(f);
            foreach (var e in History.Of(r, new[] { DiplomacyMath.Ward }, Now - (int)(WardMath.PeopleGapDays * WardMath.DayTicks)))
                if (e.actors.Any(a => a.who == r && a.role == HistoryRole.Did))
                    return e.tick;
            return -1;
        }

        /// <summary>Once a day (Diplomacy's day): a people that may now and then offers a ward to a home, by their letter.</summary>
        public static void Daily(int now)
        {
            if (!On || !Tell.CanAsk) return;
            var book = DiplomacyBook.Instance;
            if (book == null) return;
            foreach (var home in Homes.All)
            {
                if (home == null || MapFacts.InVacuum(home)) continue;
                var h = book.HomeOf(home);
                if (h.wards.Any(w => w != null && (w.kind == WardKind.Theirs || w.kind == WardKind.Ours)) || h.wards.Count >= WardMath.MaxPerHome) continue;
                bool guardian = KnownFor(home) != null;
                if (!guardian) continue;
                foreach (var f in Find.FactionManager.AllFactionsListForReading.InRandomOrder())
                {
                    if (f == null || f.IsPlayer || !Diplomacy.Deals(f) || !f.HasGoodwill) continue;
                    var head = FavourCalls.HeadOf(f, home);
                    if (head == null || Kin(f, home, out _) == null) continue;
                    int last = LastOf(f);
                    if (h.wardOfferPeople == f.loadID && h.wardOfferAt > last) last = h.wardOfferAt;
                    if (!WardMath.MayOffer(Diplomacy.Hostile(f), NotableBook.Opinion(head), guardian, false, last, h.wardOfferAt, now)) continue;
                    if (!WardMath.OffersToday(Rand.Value)) continue;
                    Offer(h, home, f);
                    return;
                }
            }
        }

        /// <summary>Their letter offering a youth of theirs for a year: accept (they come in a day or two), decline, or leave it a while.</summary>
        public static void Offer(DiplomacyHome h, Map home, Faction f)
        {
            var def = WardDefOf.Rimfolk_WardOffer;
            var kin = Kin(f, home, out var town);
            var skill = KnownFor(home);
            if (def == null || h == null || kin == null || skill == null) return;
            h.wardOfferAt = Now;
            h.wardOfferPeople = f.loadID;
            var head = FavourCalls.HeadOf(f, home);
            var form = WardWords.Form(home, f);
            string who = head != null ? NotableBook.Called(head) : WardWords.People(f);
            var guardian = Guardians(home, skill).FirstOrDefault();
            string text = WardWords.T("CC_Ward_Offer", who.Named("WHO"), WardWords.People(f).Named("PEOPLE"), kin.Name.Named("KIN"), form.word.Named("WORD"),
                              form.rite.Named("RITE"), skill.label.Named("SKILL"))
                          + "\n\n" + WardWords.P(WardMath.WardAge(ChildrenHere).min < 13f ? "CC_Ward_Offer_HowChild" : "CC_Ward_Offer_How",
                              (guardian?.LabelShort ?? "?").Named("GUARDIAN"), skill.label.Named("SKILL"));
            var letter = LetterMaker.MakeLetter(WardWords.P("CC_Ward_Offer_Label", WardWords.People(f).Named("PEOPLE")), text, def,
                town != null ? new LookTargets(town) : LookTargets.Invalid, f);
            if (letter is ChoiceLetter_WardOffer l)
            {
                l.people = f;
                l.home = Homes.KeyOf(home);
                l.notable = kin.id;
                l.skill = skill.defName;
            }
            Tell.Ask(letter, home);
            Diplomacy.Dirty();
        }

        /// <summary>Children are made where the game has them (Biotech, and the storyteller allows them).</summary>
        public static bool ChildrenHere => ModsConfig.BiotechActive && (Find.Storyteller?.difficulty?.ChildrenAllowed ?? false);

        /// <summary>The colony asked by letter for a ward to learn this skill: they send one by their head's regard. Returns their words.</summary>
        public static string HearAsk(Faction f, Map home, int regard, string skill, float days, out bool yes)
        {
            yes = false;
            if (AskWhyNot(f, home) != null) return WardWords.T("CC_Ward_Ans_Moot", WardWords.People(f).Named("PEOPLE"));
            var form = WardWords.Form(home, f);
            if (!WardMath.SendsWhenAsked(Diplomacy.Hostile(f), regard, Rand.Value))
                return WardWords.T("CC_Ward_Ans_No", WardWords.People(f).Named("PEOPLE"), form.word.Named("WORD"));
            var w = Agree(home, f, Kin(f, home, out _), skill, true, days);
            if (w == null) return WardWords.T("CC_Ward_Ans_Moot", WardWords.People(f).Named("PEOPLE"));
            yes = true;
            return WardWords.T("CC_Ward_Ans_Yes", WardWords.People(f).Named("PEOPLE"), w.pawn.Named("WARD"), form.rite.Named("RITE"), WardWords.Skill(w.skill).Named("SKILL"));
        }

        /// <summary>
        /// A ward agreed (their offer accepted, or the colony's letter answered yes): a youth of the notable's house is made
        /// (their child where the ages fit), kept in the world until they come, and due one to two days on; the ledger's Ward
        /// tie from their people to the colony. Null when one is under way at that home already or nobody of theirs can come.
        /// </summary>
        public static WardStay Agree(Map home, Faction f, Notable kin, string skill, bool asked, float days)
        {
            var book = DiplomacyBook.Instance;
            if (book == null || home == null || f == null) return null;
            var h = book.HomeOf(home);
            if (!WardMath.Fits(h.wards.Select(x => (x.kind, x.guarantees)).ToList(), WardKind.Theirs, -1)) return null;
            var youth = MakeYouth(f, kin, ChildrenHere);
            if (youth == null) return null;
            var w = new WardStay
            {
                id = book.NextId(), kind = WardKind.Theirs, stage = WardStage.Coming, pawn = youth, people = f, skill = skill ?? KnownFor(home)?.defName,
                agreed = Now, due = Now + Mathf.RoundToInt(Mathf.Max(0.05f, days) * WardMath.DayTicks), asked = asked, notable = kin?.id ?? -1, name = youth.LabelShort
            };
            h.wards.Add(w);
            WorldLedger.Keep(youth, KeepFor.Ward);
            var t = WorldLedger.AddTie(Tie.Kind.Ward, Refs.Of(f), Diplomacy.Colony(home), new TieTerms { about = Refs.Of(youth), key = w.skill, count = w.id }, Tie.Never,
                WardMath.WardOrigin);
            if (t != null) w.tie = t.id;
            Diplomacy.Write(DiplomacyMath.Ward, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(Refs.Of(youth), HistoryRole.About), new HistoryActor(Diplomacy.Colony(home), HistoryRole.For)
            }, Diplomacy.Colony(home), WardWords.P("CC_Ward_Hist_Agreed", youth.Named("WARD"), WardWords.People(f).Named("PEOPLE"), WardWords.Form(home, f).word.Named("WORD")));
            Diplomacy.Dirty();
            return w;
        }

        /// <summary>
        /// A youth of their people (a child of 9 to 12 where the game has children, else 14 to 19), of the notable's house:
        /// their child where the ages fit, else of their name. Kept in the world.
        /// </summary>
        public static Pawn MakeYouth(Faction f, Notable kin, bool child)
        {
            try
            {
                var kind = Arrivals.PersonKind(f);
                if (kind == null) return null;
                var (min, max) = WardMath.WardAge(child);
                var parent = kin?.pawn;
                string last = parent?.Name is NameTriple tri ? tri.Last : null;
                var p = Arrivals.Make(kind, f, new FloatRange(min, max), null, last, child, parent);
                if (p == null) return null;
                if (parent != null && !parent.Dead && parent.RaceProps?.Humanlike == true
                    && WardMath.CanBeParent(parent.ageTracker.AgeBiologicalYearsFloat, p.ageTracker.AgeBiologicalYearsFloat))
                {
                    try
                    {
                        p.relations?.AddDirectRelation(PawnRelationDefOf.Parent, parent);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("wards: the youth's parent", e);
                    }
                }
                if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: a youth of theirs", e);
                return null;
            }
        }

        // ================= The hour =================

        /// <summary>The hour (Diplomacy.Hourly): each ward and hostage a step on, either way.</summary>
        public static void Hourly(int now)
        {
            foreach (var (h, w) in All())
            {
                try
                {
                    if (w.Theirs) StepTheirs(h, w, now);
                    else WardsAbroad.Step(h, w, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("wards: the hour", e);
                }
            }
        }

        private static void StepTheirs(DiplomacyHome h, WardStay w, int now)
        {
            var map = h.Map;
            var f = w.people;
            var p = w.pawn;
            if (map == null || f == null || p == null || p.Discarded)
            {
                Clear(h, w, false);
                return;
            }
            switch (w.stage)
            {
                case WardStage.Coming:
                    Coming(h, w, map, now);
                    return;
                case WardStage.Here:
                case WardStage.Forfeit:
                    if (!StillHere(h, w, map)) return;
                    Tally(w, map);
                    if (w.stage == WardStage.Forfeit)
                    {
                        Hostages.ForfeitHour(h, w, map, now);
                        return;
                    }
                    if (w.Hostage)
                    {
                        Hostages.TermHour(h, w, map);
                        return;
                    }
                    Guardian(w, map);
                    Lesson(w, map);
                    if (now >= w.until) StartGoing(h, w, map);
                    return;
                case WardStage.Going:
                    Going(h, w, map, now);
                    return;
                default:
                    Clear(h, w, false);
                    return;
            }
        }

        // ---------------- Coming ----------------

        /// <summary>Due and the land quiet: the ward (or hostage) walks in with one of their kin to the gate; at the gate they join the colony.</summary>
        private static void Coming(DiplomacyHome h, WardStay w, Map map, int now)
        {
            var f = w.people;
            var lord = LordOf(w, map);
            if (lord != null)
            {
                var job = lord.LordJob as LordJob_WardParty;
                if (job != null && job.arrived >= 0 && w.pawn.Spawned && !w.pawn.Dead && !w.pawn.Downed) Join(h, w, map, job);
                else if (job != null && job.harmed) Called(h, w, map, "CC_Ward_Msg_Harmed");
                return;
            }
            if (w.pawn.Spawned)
            {
                // On the map without their party (it scattered): they join where they stand once they are on their feet.
                if (!w.pawn.Dead && !w.pawn.Downed && w.pawn.Map == map) Join(h, w, map, null);
                return;
            }
            if (w.pawn.Dead || (!w.Hostage && Diplomacy.Hostile(f)) || f.defeated)
            {
                Called(h, w, map, "CC_Ward_Msg_NotComing");
                return;
            }
            if (now < w.due || MapFacts.ThreatOn(map)) return;
            if (now - w.due > 5 * WardMath.DayTicks)
            {
                Called(h, w, map, "CC_Ward_Msg_NotComing");
                return;
            }
            Arrive(h, w, map);
        }

        /// <summary>Their people at war with the colony need a people at peace with both to vouch for them on its land (TruceParley.Escort).</summary>
        public static Faction EscortFor(Faction f) => Diplomacy.Hostile(f) ? TruceParley.Escort(f) : null;

        /// <summary>The ward and one of their kin walk in from the edge to the gate.</summary>
        private static void Arrive(DiplomacyHome h, WardStay w, Map map)
        {
            var f = w.people;
            var escort = EscortFor(f);
            if (Diplomacy.Hostile(f) && escort == null)
            {
                Called(h, w, map, "CC_Hostage_Msg_NoEscort");
                return;
            }
            if (!Arrivals.TryEntryCell(map, out var entry)) return;
            var gate = DropCellFinder.TradeDropSpot(map);
            if (!gate.IsValid) return;
            var party = new List<Pawn>();
            try
            {
                if (!Arrivals.Spawn(w.pawn, map, entry)) return;
                party.Add(w.pawn);
                var kinKind = Arrivals.PersonKind(f);
                var parent = NotableBook.Instance?.Get(w.notable)?.pawn;
                if (kinKind != null)
                {
                    var k = Arrivals.Make(kinKind, f, new FloatRange(25f, 55f), null, (w.pawn.Name as NameTriple)?.Last, false, parent);
                    GenSpawn.Spawn(k, CellFinder.RandomClosewalkCellNear(entry, map, 3), map);
                    party.Add(k);
                }
                if (escort != null)
                    foreach (var p in party)
                        p.guest?.SetGuestStatus(escort);
                LordMaker.MakeNewLord(f, new LordJob_WardParty(w.id, Homes.KeyOf(map), gate, false, escort), map, party);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: their arrival", e);
                return;
            }
            WorldLedger.Release(w.pawn, w.Hostage ? KeepFor.Hostage : KeepFor.Ward);
            Diplomacy.Dirty();
            // apart: the ward walks in with their kin toward the gate; the message looks at them.
            Tell.Message(NewsKind.World, Importance.Notable, map,
                w.Hostage
                    ? WardWords.P("CC_Hostage_Msg_Comes", w.pawn.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE"), WardWords.HostageForm(map, f).word.Named("WORD"))
                    : WardWords.P("CC_Ward_Msg_Comes", w.pawn.Named("WARD"), WardWords.People(f).Named("PEOPLE"), WardWords.Form(map, f).word.Named("WORD")),
                new LookTargets(w.pawn), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// At the gate: the ward leaves their kin's group and joins the colony as the game's lodger (the quest), a guardian
        /// is chosen, and the year (or the term) starts; their kin go home.
        /// </summary>
        private static void Join(DiplomacyHome h, WardStay w, Map map, LordJob_WardParty job)
        {
            var f = w.people;
            var p = w.pawn;
            try
            {
                p.GetLord()?.RemovePawn(p);
                if (!Arrivals.Join(p, map)) return;
                StartQuest(w, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: joining the colony", e);
                return;
            }
            if (job != null) job.done = true;
            w.stage = WardStage.Here;
            w.since = Now;
            // A hostage keeps the term noted at the giving (a pact's year: Hostages.EndOf); a ward stays a year from now.
            w.until = w.Hostage ? w.until : Now + Mathf.RoundToInt(WardMath.YearDays * WardMath.DayTicks);
            w.injuries = Injuries(p);
            var skill = SkillOf(w);
            w.xpAtStart = skill != null ? p.skills?.GetSkill(skill)?.XpTotalEarned ?? 0f : 0f;
            if (!w.Hostage) w.guardian = skill != null ? Guardians(map, skill, p).FirstOrDefault() : null;
            Diplomacy.Dirty();
            if (w.Hostage)
            {
                Hostages.Arrived(h, w, map);
                return;
            }
            var form = WardWords.Form(map, f);
            Diplomacy.Write(DiplomacyMath.Ward, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(p), HistoryRole.About), new HistoryActor(Diplomacy.Colony(map), HistoryRole.With), new HistoryActor(Refs.Of(f), HistoryRole.For)
            }, Diplomacy.Colony(map), WardWords.P("CC_Ward_Hist_Came", p.Named("WARD"), WardWords.People(f).Named("PEOPLE"), form.word.Named("WORD")), true);
            string text = WardWords.T("CC_Ward_Arrives", p.Named("WARD"), WardWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"),
                WardWords.Skill(w.skill).Named("SKILL"));
            text += "\n\n" + (w.guardian != null
                ? WardWords.P(AtGameLessons(p) ? "CC_Ward_Arrives_GuardianChild" : "CC_Ward_Arrives_Guardian", w.guardian.Named("GUARDIAN"), p.Named("WARD"),
                    WardWords.Skill(w.skill).Named("SKILL"))
                : WardWords.P("CC_Ward_Arrives_NoGuardian", p.Named("WARD"), WardWords.Skill(w.skill).Named("SKILL")));
            text += "\n\n" + WardWords.P("CC_Ward_Arrives_How", p.Named("WARD"));
            Tell.Letter(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Ward_Arrives_Label", p.Named("WARD")), text, LetterDefOf.PositiveEvent, new LookTargets(p), f);
            if (w.guardian != null) Outsiders.Say(p, "wardmeet", w.guardian);
        }

        /// <summary>
        /// The game's lodger: a quest of Rimfolk's own (Rimfolk_LodgerQuest, in the Quests tab) with the game's
        /// QuestPart_ExtraFaction, their people the home people, as the game's refugee hosting does. The part is in the quest
        /// before the game takes the quest in (QuestManager.Add fills its list of extra-faction parts then), and the quest is
        /// under way from the start. No part of Rimfolk's own is in it: the terms are the record's and the tie's, so a save
        /// without Rimfolk keeps only the game's own parts.
        /// </summary>
        public static void StartQuest(WardStay w, Map map)
        {
            var root = DefDatabase<QuestScriptDef>.GetNamedSilentFail(QuestRoot);
            if (root == null || Find.QuestManager == null || w?.pawn == null || w.people == null) return;
            var f = w.people;
            var form = w.Hostage ? WardWords.HostageForm(map, f) : WardWords.Form(map, f);
            var quest = Quest.MakeRaw();
            quest.root = root;
            quest.name = w.Hostage
                ? WardWords.P("CC_Hostage_Quest_Title", w.pawn.Named("HOSTAGE"), form.word.Named("WORD"))
                : WardWords.P("CC_Ward_Quest_Title", w.pawn.Named("WARD"), form.word.Named("WORD"));
            quest.description = w.Hostage
                ? WardWords.P("CC_Hostage_Quest_Text", w.pawn.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE"), form.rite.Named("RITE"))
                : WardWords.P("CC_Ward_Quest_Text", w.pawn.Named("WARD"), WardWords.People(f).Named("PEOPLE"), WardWords.Skill(w.skill).Named("SKILL"), form.rite.Named("RITE"));
            quest.challengeRating = 1;
            quest.appearanceTick = Now;
            quest.AddPart(new QuestPart_ExtraFaction
            {
                affectedPawns = new List<Pawn> { w.pawn }, extraFaction = new ExtraFaction(f, ExtraFactionType.HomeFaction), areHelpers = false
            });
            quest.SetInitiallyAccepted();
            Find.QuestManager.Add(quest);
            w.quest = quest.id;
        }

        public static Quest QuestOf(WardStay w) => w == null || w.quest < 0 ? null : Find.QuestManager?.QuestsListForReading.FirstOrDefault(q => q.id == w.quest);

        /// <summary>The lodger's quest over (once they are no longer the colony's lodger, or have joined it for good).</summary>
        public static void EndQuest(WardStay w, QuestEndOutcome outcome)
        {
            var q = QuestOf(w);
            if (q == null) return;
            try
            {
                if (q.State == QuestState.Ongoing || q.State == QuestState.NotYetAccepted) q.End(outcome, false, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: the quest's end", e);
            }
        }

        /// <summary>The ward or hostage won't come after all: let go, told.</summary>
        private static void Called(DiplomacyHome h, WardStay w, Map map, string key)
        {
            var name = WardWords.Name(w);
            var f = w.people;
            var lord = LordOf(w, map);
            if (lord?.LordJob is LordJob_WardParty j) j.done = true;
            if (w.tie >= 0) WorldLedger.Set(WorldLedger.Get(w.tie), Tie.State.Ended);
            Clear(h, w, false);
            // apart: the ward or hostage agreed won't come; told in a message.
            Tell.Message(NewsKind.World, Importance.Notable, map, WardWords.P(key, name.Named("WARD"), WardWords.People(f).Named("PEOPLE")), LookTargets.Invalid,
                MessageTypeDefOf.NeutralEvent);
        }

        // ---------------- Here ----------------

        public static SkillDef SkillOf(WardStay w) => w?.skill.NullOrEmpty() ?? true ? null : DefDatabase<SkillDef>.GetNamedSilentFail(w.skill);

        private static int Injuries(Pawn p) => p?.health?.hediffSet?.hediffs?.Count(x => x is Hediff_Injury) ?? 0;

        /// <summary>A colonist killed their ward (Patch_WardKilled): a death at the colony's hand, a feud.</summary>
        private static readonly HashSet<int> killedByColony = new HashSet<int>();

        internal static void Killed(Pawn p, DamageInfo? dinfo)
        {
            if (p == null || Of(p) == null) return;
            if (dinfo?.Instigator is Pawn a && a != p && a.Faction == Faction.OfPlayer && a.RaceProps?.Humanlike == true && Of(a) == null) killedByColony.Add(p.thingIDNumber);
        }

        /// <summary>
        /// Still the colony's lodger: dead, carried off, taken prisoner by the colony, or no longer its (sent away) each ends
        /// the year as it stands. Returns whether they are still here.
        /// </summary>
        private static bool StillHere(DiplomacyHome h, WardStay w, Map map)
        {
            var p = w.pawn;
            if (p.Dead)
            {
                Died(h, w, map);
                return false;
            }
            if (People.Kidnapped(p))
            {
                Taken(h, w, map);
                return false;
            }
            if (p.IsPrisonerOfColony)
            {
                if (w.Hostage && w.stage == WardStage.Forfeit)
                {
                    Hostages.Arrested(h, w, map);
                    return false;
                }
                Wronged(h, w, map, "CC_Ward_End_Arrested");
                return false;
            }
            if (p.Faction != Faction.OfPlayer)
            {
                // A forfeit hostage gone (fled, or out at the edge): slipped away; anyone else sent away before the term was out.
                if (w.Hostage && w.stage == WardStage.Forfeit) Hostages.SlippedAway(h, w, map);
                else Wronged(h, w, map, "CC_Ward_End_SentAway");
                return false;
            }
            return true;
        }

        /// <summary>The day's tally, once the day turns: a day here, their mood, whether they went hungry, new injuries.</summary>
        private static void Tally(WardStay w, Map map)
        {
            var p = w.pawn;
            int today = GenLocalDate.DayOfYear(map) + GenLocalDate.Year(map) * GenDate.DaysPerYear;
            if (w.dayTallied == today) return;
            w.dayTallied = today;
            w.days++;
            w.moodSum += p.needs?.mood?.CurLevelPercentage ?? 0.5f;
            if (p.needs?.food != null && p.needs.food.CurCategory >= HungerCategory.Hungry) w.hungryDays++;
            int hurt = Injuries(p);
            if (hurt > w.injuries)
            {
                w.hurt += hurt - w.injuries;
                if (!w.Hostage && p.Spawned)
                {
                    bool sheltered = !(p.GetRoom()?.PsychologicallyOutdoors ?? true);
                    // apart: their people will hear of it; told once it is seen.
                    Tell.Minor(NewsKind.World, Importance.Notable, map, WardWords.P(sheltered ? "CC_Ward_Msg_HurtInside" : "CC_Ward_Msg_HurtOutside", p.Named("WARD"),
                        WardWords.People(w.people).Named("PEOPLE")), new LookTargets(p));
                }
            }
            w.injuries = hurt;
        }

        /// <summary>Their guardian gone (dead, left, no longer the colony's): none, until the player picks another (This week asks).</summary>
        private static void Guardian(WardStay w, Map map)
        {
            var g = w.guardian;
            if (g == null) return;
            if (g.Dead || g.Discarded || g.Faction != Faction.OfPlayer || g.IsPrisoner || People.Kidnapped(g) || g.IsQuestLodger())
            {
                w.guardian = null;
                Diplomacy.Dirty();
                // apart: the ward is without a teacher; the player picks another on their card.
                Tell.Message(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Ward_Msg_NoGuardian", w.pawn.Named("WARD")), new LookTargets(w.pawn), MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>Up and about, with nothing more pressing: in their free time, no danger, not drafted, not asleep.</summary>
        public static bool Free(Pawn p) =>
            Together.Able(p) && !p.InBed() && Together.FreeTime(p) && PlayOuts.Interruptible(p) && p.Map.dangerWatcher.DangerRating == StoryDanger.None
            && (p.needs?.food == null || p.needs.food.CurCategory < HungerCategory.UrgentlyHungry) && (p.needs?.rest == null || p.needs.rest.CurLevel > 0.2f);

        /// <summary>
        /// The day's lesson: once a day, by day, when the ward and their guardian are both free and together (the moment
        /// waits for them, and the ward may go and find the guardian in their free time); then the ward walks over and the
        /// guardian teaches them an hour. A child with Biotech learns at the game's own lessons instead.
        /// </summary>
        private static void Lesson(WardStay w, Map map)
        {
            var p = w.pawn;
            var g = w.guardian;
            var skill = SkillOf(w);
            var def = WardDefOf.Rimfolk_WardLesson;
            if (g == null || skill == null || def == null || AtGameLessons(p) || !p.Spawned || !g.Spawned || g.Map != p.Map) return;
            int today = GenLocalDate.DayOfYear(map) + GenLocalDate.Year(map) * GenDate.DaysPerYear;
            if (!WardMath.LessonDue(w.lessonDay, today, GenLocalDate.HourOfDay(map))) return;
            if (p.CurJobDef == def) return;
            string key = "wardlesson|" + w.id;
            // together: the lesson waits until the two are together and free; the ward walks over to start it.
            Together.When(key, p, g, () => StartLesson(w), 6f, 1f, () => Free(p) && Free(g));
        }

        private static void StartLesson(WardStay w)
        {
            var p = w.pawn;
            var g = w.guardian;
            var def = WardDefOf.Rimfolk_WardLesson;
            if (def == null || p == null || g == null || w.stage != WardStage.Here) return;
            var job = JobMaker.MakeJob(def, g);
            job.count = DefDatabase<SkillDef>.AllDefsListForReading.IndexOf(SkillOf(w));
            if (job.count < 0) return;
            p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>A lesson begun (JobDriver_WardLesson): counted for the day.</summary>
        public static void LessonBegun(Pawn ward)
        {
            var w = Of(ward, out var h);
            var map = h?.Map;
            if (w == null || map == null) return;
            w.lessonDay = GenLocalDate.DayOfYear(map) + GenLocalDate.Year(map) * GenDate.DaysPerYear;
            w.lessons++;
            Diplomacy.Dirty();
        }

        // ---------------- Going home ----------------

        /// <summary>The year is over: their kin come to the gate for them (a people at war: the ward walks out alone).</summary>
        public static void StartGoing(DiplomacyHome h, WardStay w, Map map)
        {
            w.stage = WardStage.Going;
            w.due = Now;
            Diplomacy.Dirty();
        }

        private static void Going(DiplomacyHome h, WardStay w, Map map, int now)
        {
            var p = w.pawn;
            if (p.Dead)
            {
                Died(h, w, map);
                return;
            }
            if (People.Kidnapped(p))
            {
                Taken(h, w, map);
                return;
            }
            if (p.Faction != Faction.OfPlayer && !p.IsPrisoner)
            {
                // Gone with their kin, or out the edge (WardHome done): nothing more to do.
                Clear(h, w, false);
                return;
            }
            if (p.IsPrisonerOfColony)
            {
                Wronged(h, w, map, "CC_Ward_End_Arrested");
                return;
            }
            // Away from home (a caravan, another map): their people take them home from there after a few days.
            if (!p.Spawned || p.Map != map)
            {
                if (now - w.due > 3 * WardMath.DayTicks) HomeFromAfar(h, w, map);
                return;
            }
            var lord = LordOf(w, map);
            if (lord == null)
            {
                if (p.CurJobDef == WardDefOf.Rimfolk_WardToKin || p.CurJobDef == WardDefOf.Rimfolk_WardLeave) return;
                bool alone = w.Hostage || Diplomacy.Hostile(w.people) || w.alone;
                if (alone)
                {
                    if (Together.Able(p)) WalkOut(w);
                    return;
                }
                if (MapFacts.ThreatOn(map)) return;
                if (!SendKin(w, map)) w.alone = true;
                return;
            }
            var job = lord.LordJob as LordJob_WardParty;
            if (job == null) return;
            if (job.harmed || job.done)
            {
                // Their kin left without them (hurt, or tired of waiting): the ward walks out alone.
                w.alone = true;
                return;
            }
            if (job.arrived < 0 || !Together.Able(p) || p.InBed() && GenLocalDate.HourOfDay(map) < 6) return;
            if (now - job.arrived > Mathf.RoundToInt(WardMath.KinWaitHours * GenDate.TicksPerHour))
            {
                job.done = true;
                return;
            }
            var kin = lord.ownedPawns.FirstOrDefault(x => x != null && x.Spawned && !x.Dead && !x.Downed && x != p);
            var def = WardDefOf.Rimfolk_WardToKin;
            if (kin == null || def == null || p.CurJobDef == def) return;
            // together: the ward walks out to their kin at the gate and leaves with them, face to face.
            p.jobs.StartJob(JobMaker.MakeJob(def, kin), JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>One of their kin (and a second where they have one) walk in to the gate for the ward.</summary>
        private static bool SendKin(WardStay w, Map map)
        {
            var f = w.people;
            if (!Arrivals.TryEntryCell(map, out var entry)) return false;
            var gate = DropCellFinder.TradeDropSpot(map);
            var kind = Arrivals.PersonKind(f);
            if (!gate.IsValid || kind == null) return false;
            var party = new List<Pawn>();
            try
            {
                var parent = NotableBook.Instance?.Get(w.notable)?.pawn;
                string last = (w.pawn.Name as NameTriple)?.Last;
                for (int i = 0; i < 2; i++)
                {
                    var k = Arrivals.Make(kind, f, new FloatRange(20f, 60f), null, last, false, parent);
                    GenSpawn.Spawn(k, CellFinder.RandomClosewalkCellNear(entry, map, 3), map);
                    party.Add(k);
                }
                LordMaker.MakeNewLord(f, new LordJob_WardParty(w.id, Homes.KeyOf(map), gate, true, null), map, party);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: their kin", e);
                foreach (var k in party)
                    if (k.Spawned) k.DeSpawn();
                foreach (var k in party) Arrivals.Discard(k);
                return false;
            }
            // apart: their kin walk in toward the gate; the message looks at them.
            Tell.Message(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Ward_Msg_KinCome", w.pawn.Named("WARD"), WardWords.People(f).Named("PEOPLE")),
                new LookTargets(party[0]), MessageTypeDefOf.NeutralEvent);
            return true;
        }

        /// <summary>The ward walks out to the edge of the colony's land alone and goes home from there (JobDriver_WardLeave).</summary>
        public static void WalkOut(WardStay w)
        {
            var p = w?.pawn;
            var def = WardDefOf.Rimfolk_WardLeave;
            if (p == null || !p.Spawned || def == null) return;
            if (!RCellFinder.TryFindBestExitSpot(p, out var edge, TraverseMode.ByPawn)) return;
            // together: the ward walks out on their own; nobody else is needed for it.
            p.jobs.StartJob(JobMaker.MakeJob(def, edge), JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>The ward met their kin at the gate (JobDriver_WardToKin): they go home together, and the year is reckoned.</summary>
        public static void MetKin(Pawn p, Pawn kin)
        {
            var w = Of(p, out var h);
            var map = h?.Map;
            if (w == null || map == null || w.stage != WardStage.Going) return;
            var lord = kin?.GetLord();
            Outsiders.Say(kin, "wardkin", p);
            Home(h, w, map, () =>
            {
                if (lord != null && lord.CanAddPawn(p)) lord.AddPawn(p);
                if (lord?.LordJob is LordJob_WardParty j) j.done = true;
            });
        }

        /// <summary>At the edge (JobDriver_WardLeave): out of the colony's land, to their people.</summary>
        public static void AtEdge(Pawn p)
        {
            var w = Of(p, out var h);
            var map = h?.Map;
            if (w == null || map == null || !p.Spawned) return;
            if (w.stage == WardStage.Forfeit)
            {
                Hostages.SlippedAway(h, w, map);
                return;
            }
            Home(h, w, map, () => p.ExitMap(false, CellRect.WholeMap(p.Map).GetClosestEdge(p.Position)));
        }

        /// <summary>Not on the home's map when the year ended: their people take them home from wherever they are.</summary>
        private static void HomeFromAfar(DiplomacyHome h, WardStay w, Map map) => Home(h, w, map, null);

        /// <summary>
        /// Home: the year (or a hostage's term) reckoned and told, then the lodger leaves the colony by the game's own leaving
        /// (LeaveQuestPartUtility: their people's again, out of any caravan), <paramref name="then"/> takes them off (with their
        /// kin, or out at the edge), and the quest ends.
        /// </summary>
        private static void Home(DiplomacyHome h, WardStay w, Map map, Action then)
        {
            var p = w.pawn;
            var f = w.people;
            if (w.Hostage) Hostages.Returned(h, w, map);
            else Reckon(h, w, map);
            try
            {
                var q = QuestOf(w);
                bool edge = then != null && p.Spawned && p.CurJobDef == WardDefOf.Rimfolk_WardLeave;
                if (!edge)
                {
                    if (q != null) LeaveQuestPartUtility.MakePawnLeave(p, q);
                    // No quest to say whose they are (a save that lost it): theirs again all the same.
                    if (p.Faction == Faction.OfPlayer && f != null && !f.defeated) p.SetFaction(f);
                }
                then?.Invoke();
                // Out at the edge as the colony's lodger: theirs again in the world.
                if (p.Faction == Faction.OfPlayer && !p.Spawned && f != null && !f.defeated) p.SetFaction(f);
                // Theirs again on a map with nobody to walk out with (on another map when the year ended): they walk out alone.
                if (p.Spawned && p.Faction != Faction.OfPlayer && !p.Downed && p.GetLord() == null)
                    LordMaker.MakeNewLord(p.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, true), p.Map, new List<Pawn> { p });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: going home", e);
            }
            EndQuest(w, QuestEndOutcome.Success);
            Clear(h, w, !w.Hostage);
        }

        /// <summary>
        /// How the year went (taught, fed, unhurt, content), and what it does: their head's regard and the ward's kin's, their
        /// people's goodwill, the deed "fostered" (a favour owed for a fine year) or "slighted", the tie kept or ended, the
        /// history, the letter.
        /// </summary>
        private static void Reckon(DiplomacyHome h, WardStay w, Map map)
        {
            var p = w.pawn;
            var f = w.people;
            int lessons = w.lessons;
            var skill = SkillOf(w);
            float xp = skill != null && w.xpAtStart >= 0f ? Mathf.Max(0f, (p.skills?.GetSkill(skill)?.XpTotalEarned ?? 0f) - w.xpAtStart) : 0f;
            if (AtGameLessons(p)) lessons = Mathf.Min(w.days, Mathf.FloorToInt(xp / WardMath.ChildDayXp));
            float mood = w.days > 0 ? w.moodSum / w.days : p.needs?.mood?.CurLevelPercentage ?? 0.5f;
            var r = WardMath.Year(lessons, w.days, w.hungryDays, w.hurt, mood);
            if (f != null && !f.defeated)
            {
                int regard = WardMath.HomeRegard(r);
                var head = FavourCalls.HeadOf(f, map);
                var kin = NotableBook.Instance?.Get(w.notable);
                string note = WardWords.P("CC_Ward_Note_Home", p.Named("WARD"), WardWords.Result(r).Named("RESULT"));
                if (head != null)
                {
                    head.personal += regard;
                    head.Remember(note, regard);
                }
                if (kin != null && kin != head && kin.Alive)
                {
                    kin.personal += regard;
                    kin.Remember(note, regard);
                }
                WorldSituationWorker.Goodwill(f, WardMath.HomeGoodwill(r), false);
                int weight = WardMath.FosteredWeight(r);
                if (weight > 0) FactionMemory.Note(f, "fostered", p.LabelShort, weight);
                else FactionMemory.Note(f, "slighted", p.LabelShort);
            }
            var t = WorldLedger.Get(w.tie);
            if (t != null && t.Live)
            {
                t.terms.number = (int)r;
                t.terms.days = xp;
                WorldLedger.Touch(t);
                WorldLedger.Set(t, r >= WardResult.Good ? Tie.State.Kept : Tie.State.Ended);
            }
            // The ward remembers the year while they live: a regard of theirs toward the colony (Grown, HeirFor, them:fostered).
            if (!p.Dead)
                WorldLedger.AddTie(Tie.Kind.Regard, Refs.Of(p), Diplomacy.Colony(map), new TieTerms { key = WardMath.FosteredKey, number = WardMath.GrownRegard(r), count = (int)r },
                    Tie.Never, WardMath.WardOrigin);
            var form = WardWords.Form(map, f);
            var yearActors = new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(p), HistoryRole.About), new HistoryActor(Diplomacy.Colony(map), HistoryRole.With), new HistoryActor(Refs.Of(f), HistoryRole.For)
            };
            // The guardian, by name: the colony's renown and their own name read a good year as a ward fostered well (Diplomacy/Renown.cs).
            if (w.guardian != null) yearActors.Add(new HistoryActor(Refs.Of(w.guardian), RenownMath.GuardianRole(r >= WardResult.Good)));
            Diplomacy.Write(DiplomacyMath.Ward, yearActors, Diplomacy.Colony(map),
                WardWords.P("CC_Ward_Hist_Home_" + r, p.Named("WARD"), WardWords.People(f).Named("PEOPLE"), form.word.Named("WORD")), r != WardResult.Fair);
            var parts = new List<string> { WardWords.T("CC_Ward_Home_" + r, p.Named("WARD"), WardWords.People(f).Named("PEOPLE"), form.word.Named("WORD")) };
            var held = new List<string>();
            if (lessons * 2 >= w.days && w.days > 0) held.Add(WardWords.P("CC_Ward_Home_Taught", WardWords.Skill(w.skill).Named("SKILL")));
            if (WardMath.Fed(w.hungryDays, w.days)) held.Add(WardWords.P("CC_Ward_Home_Fed"));
            if (w.hurt <= 0) held.Add(WardWords.P("CC_Ward_Home_Unhurt"));
            if (mood >= 0.55f) held.Add(WardWords.P("CC_Ward_Home_Content"));
            if (held.Count > 0) parts.Add(WardWords.P("CC_Ward_Home_Held", p.Named("WARD"), TextUtil.JoinAnd(held).Named("WHAT")));
            if (skill != null && xp > 0f) parts.Add(WardWords.P("CC_Ward_Home_Learned", p.Named("WARD"), skill.label.Named("SKILL"), (p.skills?.GetSkill(skill)?.Level ?? 0).Named("LEVEL")));
            if (r == WardResult.Fine) parts.Add(WardWords.P("CC_Ward_Home_Favour", WardWords.People(f).Named("PEOPLE")));
            Tell.Letter(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Ward_Home_Label", p.Named("WARD")), string.Join("\n\n", parts),
                r >= WardResult.Good ? LetterDefOf.PositiveEvent : r == WardResult.Poor ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent,
                p.Spawned ? new LookTargets(p) : LookTargets.Invalid, f);
            if (w.guardian != null && w.guardian.Spawned && p.Spawned && Together.Can(p, w.guardian)) Outsiders.Say(p, "wardbye", w.guardian);
        }

        // ---------------- Hurt, killed, taken ----------------

        /// <summary>Their ward (or hostage) died in the colony's care: their grief by whether it sheltered them; a feud at a colonist's hand.</summary>
        private static void Died(DiplomacyHome h, WardStay w, Map map)
        {
            var p = w.pawn;
            var f = w.people;
            var room = p.Corpse?.GetRoom();
            bool sheltered = room != null && !room.PsychologicallyOutdoors;
            bool byColony = killedByColony.Remove(p.thingIDNumber);
            if (f != null && !f.defeated)
            {
                int regard = WardMath.DiedRegard(sheltered, byColony);
                var head = FavourCalls.HeadOf(f, map);
                var kin = NotableBook.Instance?.Get(w.notable);
                string note = WardWords.P("CC_Ward_Note_Died", p.Named("WARD"));
                if (head != null)
                {
                    head.personal += regard;
                    head.Remember(note, regard);
                }
                if (kin != null && kin != head && kin.Alive)
                {
                    kin.personal += regard;
                    kin.Remember(note, regard);
                }
                WorldSituationWorker.Goodwill(f, WardMath.DiedGoodwill(sheltered, byColony), false);
                FactionMemory.Note(f, "slighted", p.LabelShort, byColony ? 20 : sheltered ? 6 : 12);
                if (WardMath.Feud(byColony))
                    WorldLedger.AddTie(Tie.Kind.Grudge, Refs.Of(f), Diplomacy.Colony(map), new TieTerms { key = WardMath.FeudKey, about = Refs.Of(p) }, Tie.Never, WardMath.WardOrigin);
            }
            var t = WorldLedger.Get(w.tie);
            if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Broken);
            EndQuest(w, QuestEndOutcome.Fail);
            Diplomacy.Write(w.Hostage ? DiplomacyMath.Hostage : DiplomacyMath.Ward, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(p), HistoryRole.About), new HistoryActor(Diplomacy.Colony(map), HistoryRole.With), new HistoryActor(Refs.Of(f), HistoryRole.For)
            }, Diplomacy.Colony(map), WardWords.P("CC_Ward_Hist_Died", p.Named("WARD"), WardWords.People(f).Named("PEOPLE")), true);
            string key = byColony ? "CC_Ward_Died_Colony" : sheltered ? "CC_Ward_Died_Inside" : "CC_Ward_Died_Outside";
            Tell.Letter(NewsKind.World, Importance.Important, map, WardWords.P("CC_Ward_Died_Label", p.Named("WARD")),
                WardWords.T(key, p.Named("WARD"), WardWords.People(f).Named("PEOPLE")), LetterDefOf.NegativeEvent,
                p.Corpse != null && p.Corpse.Spawned ? new LookTargets(p.Corpse) : LookTargets.Invalid, f);
            Clear(h, w, false);
        }

        /// <summary>Carried off by raiders: their people grieve as for one left in the open; the game's own captivity has them.</summary>
        private static void Taken(DiplomacyHome h, WardStay w, Map map)
        {
            var p = w.pawn;
            var f = w.people;
            if (f != null && !f.defeated)
            {
                var head = FavourCalls.HeadOf(f, map);
                int regard = WardMath.DiedRegard(false, false);
                if (head != null)
                {
                    head.personal += regard;
                    head.Remember(WardWords.P("CC_Ward_Note_Taken", p.Named("WARD")), regard);
                }
                FactionMemory.Note(f, "slighted", p.LabelShort, 10);
            }
            var t = WorldLedger.Get(w.tie);
            if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Broken);
            EndQuest(w, QuestEndOutcome.Fail);
            Tell.Letter(NewsKind.World, Importance.Important, map, WardWords.P("CC_Ward_Taken_Label", p.Named("WARD")),
                WardWords.T("CC_Ward_Taken", p.Named("WARD"), WardWords.People(f).Named("PEOPLE")), LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
            Clear(h, w, false);
        }

        /// <summary>Taken prisoner by the colony or sent away before the year was out: an insult their people remember.</summary>
        private static void Wronged(DiplomacyHome h, WardStay w, Map map, string key)
        {
            var p = w.pawn;
            var f = w.people;
            if (f != null && !f.defeated)
            {
                var head = FavourCalls.HeadOf(f, map);
                int regard = WardMath.DiedRegard(false, false);
                if (head != null)
                {
                    head.personal += regard;
                    head.Remember(WardWords.P("CC_Ward_Note_Wronged", p.Named("WARD")), regard);
                }
                FactionMemory.Note(f, "slighted", p.LabelShort, 12);
            }
            // A prisoner of the colony's is their people's again (the quest no longer makes them a lodger): ransom applies.
            try
            {
                if (p.IsPrisonerOfColony && p.Faction == Faction.OfPlayer && f != null && !f.defeated) p.SetFaction(f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: a prisoner's people", e);
            }
            var t = WorldLedger.Get(w.tie);
            if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Broken);
            EndQuest(w, QuestEndOutcome.Fail);
            Tell.Letter(NewsKind.World, Importance.Important, map, WardWords.P("CC_Ward_Wronged_Label", p.Named("WARD")),
                WardWords.T(key, p.Named("WARD"), WardWords.People(f).Named("PEOPLE")), LetterDefOf.NegativeEvent, p.Spawned ? new LookTargets(p) : LookTargets.Invalid, f);
            Clear(h, w, false);
        }

        // ================= Acts =================

        /// <summary>Why the player can't pick a guardian for this ward now, or null.</summary>
        public static string GuardianWhyNot(WardStay w)
        {
            if (w == null || w.kind != WardKind.Theirs || w.stage != WardStage.Here) return WardWords.P("CC_Ward_Why_NotHere");
            if (AtGameLessons(w.pawn)) return WardWords.P("CC_Ward_Why_GameLessons", w.pawn.Named("WARD"));
            Of(w.pawn, out var h);
            return Guardians(h?.Map, SkillOf(w), w.pawn).Count == 0 ? WardWords.P("CC_Ward_Why_NoGuardian", WardMath.GuardianSkill.Named("SKILL")) : null;
        }

        /// <summary>The player picks the ward's guardian (a menu of those with 10 or more in the skill).</summary>
        public static void ChooseGuardian(WardStay w)
        {
            Of(w?.pawn, out var h);
            var map = h?.Map;
            var skill = SkillOf(w);
            if (map == null || skill == null) return;
            var opts = new List<FloatMenuOption>();
            foreach (var g in Guardians(map, skill, w.pawn).Take(12))
            {
                var c = g;
                opts.Add(new FloatMenuOption(WardWords.P("CC_Ward_Guardian_Pick", c.Named("PAWN"), skill.label.Named("SKILL"), c.skills.GetSkill(skill).Level.Named("LEVEL")), () =>
                {
                    w.guardian = c;
                    Diplomacy.Dirty();
                    // apart: the player's choice, confirmed in a message; the lessons play out on the map from now on.
                    Tell.Message(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Ward_Msg_GuardianSet", c.Named("GUARDIAN"), w.pawn.Named("WARD")), new LookTargets(c),
                        MessageTypeDefOf.NeutralEvent);
                }));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption(WardWords.P("CC_Ward_Why_NoGuardian", WardMath.GuardianSkill.Named("SKILL")), null));
            Find.WindowStack.Add(new FloatMenu(opts, WardWords.P("CC_Ward_Guardian_Title", w.pawn.Named("WARD"))));
        }

        /// <summary>Their ward sent home before the year is out (the card's act): a slight, said as such.</summary>
        public static void SendHome(WardStay w)
        {
            Of(w?.pawn, out var h);
            var map = h?.Map;
            if (w == null || map == null || w.kind != WardKind.Theirs || w.stage != WardStage.Here) return;
            w.moodSum = Mathf.Min(w.moodSum, 0.2f * Mathf.Max(1, w.days));
            StartGoing(h, w, map);
        }

        // ================= The grown ward =================

        /// <summary>
        /// A notable added (Notables.Add): one who was fostered at the colony thinks as well of it as the year earned
        /// (WardMath.GrownRegard), and remembers why.
        /// </summary>
        public static void Grown(Notable n)
        {
            try
            {
                var t = Fostered(n?.pawn);
                if (t == null) return;
                int regard = WardMath.GrownRegard(ResultOf(t));
                n.personal += regard;
                n.Remember(WardWords.P("CC_Ward_Note_Grown"), regard);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: the grown ward", e);
            }
        }

        /// <summary>A town of theirs in want of an heir: a ward fostered at the colony, grown now (18), of that town's people, and in the world.</summary>
        public static Pawn HeirFor(Settlement s)
        {
            if (s?.Faction == null || WorldLedger.Instance == null) return null;
            try
            {
                foreach (var t in WorldLedger.OwnTies(Tie.Kind.Regard).ToList())
                {
                    if (t == null || !t.Live || t.origin != WardMath.WardOrigin || t.terms?.key != WardMath.FosteredKey || ResultOf(t) < WardResult.Good) continue;
                    var p = Refs.Pawn(t.from);
                    if (p == null || p.Dead || p.Discarded || p.Spawned || p.Faction != s.Faction || p.ageTracker.AgeBiologicalYearsFloat < WardMath.HeirAge) continue;
                    if (NotableBook.Instance?.notables.Any(n => n != null && n.pawn == p) ?? true) continue;
                    if (!WorldSituationWorker.FreeInWorld(p)) continue;
                    return p;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("wards: a grown ward as heir", e);
            }
            return null;
        }

        // ================= Letting go =================

        /// <summary>
        /// The record let go: its keep reasons (a ward fostered and gone home stays kept as such while the ledger remembers
        /// their tie: KeepFor.Ward's daily check lets them go after), and the lesson waiting.
        /// </summary>
        public static void Clear(DiplomacyHome h, WardStay w, bool fostered)
        {
            if (h == null || w == null) return;
            h.wards.Remove(w);
            Together.Forget("wardlesson|" + w.id);
            var p = w.pawn;
            if (p != null)
            {
                WorldLedger.Release(p, KeepFor.Hostage);
                WorldLedger.Release(p, KeepFor.Lent);
                if (fostered && !p.Dead && w.Theirs) WorldLedger.Keep(p, KeepFor.Ward);
                else if (WorldLedger.Release(p, KeepFor.Ward) && !p.Spawned && !p.Dead && w.Theirs && w.stage == WardStage.Coming) Arrivals.Discard(p);
            }
            Diplomacy.Dirty();
        }

        /// <summary>A home is no longer the colony's: theirs there go home (their people's again), ours away come home to another.</summary>
        internal static void HomeLost(DiplomacyHome h)
        {
            if (h?.wards == null) return;
            foreach (var w in h.wards.ToList())
            {
                try
                {
                    if (w.Theirs)
                    {
                        var q = QuestOf(w);
                        if (w.pawn != null && !w.pawn.Dead && w.pawn.Faction == Faction.OfPlayer && q != null) LeaveQuestPartUtility.MakePawnLeave(w.pawn, q);
                        EndQuest(w, QuestEndOutcome.Unknown);
                        Clear(h, w, false);
                    }
                    else WardsAbroad.HomeLost(h, w);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("wards: a home lost", e);
                }
            }
        }

        // ================= Words =================

        /// <summary>Why they are here, for Here and away ("living at the colony for a year as a ward"), or null for anyone else.</summary>
        public static string HereAs(Pawn p)
        {
            var w = Of(p, out var h);
            if (w == null || !w.Theirs || w.stage == WardStage.Coming) return null;
            var home = h?.Map ?? Homes.Seat;
            return w.Hostage
                ? WardWords.P(w.stage == WardStage.Forfeit ? "CC_Hostage_HereAs_Forfeit" : "CC_Hostage_HereAs", WardWords.HostageForm(home, w.people).word.Named("WORD"))
                : WardWords.P("CC_Ward_HereAs", WardWords.Form(home, w.people).word.Named("WORD"), WardWords.Mood(Mood(w)).Named("MOOD"));
        }
    }
}
