using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWorld.Planet;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Their envoy's season at the colony (docs/design/stage2-diplomacy.md, round a, item 4; release 2a.3; decision 6; the
    /// rules in EnvoyMath.cs). A people at peace (goodwill 30 or more) whose envoy the colony knows may offer to send them,
    /// at most once a year per people and a season apart at a home, one envoy at a time per home (their letter); or the
    /// colony asks by letter. Agreed, the envoy walks in with an attendant (a guard of theirs) and stays the season as the
    /// colony's guest who does no work (LordJob_EnvoyStay): about its common rooms by day, indoors in danger, eating at its
    /// table and sleeping in the bed set aside for them (or a spare one, or the floor) by the missions' hosting rules
    /// (Missions/Hosting.cs), talking with the colonists (the outsiders' own talks), sitting in at a council meeting that
    /// falls during the stay, and handing a letter home to their runner at the gate every five days. How it goes is scored
    /// day by day and shown as a word; at the end their report moves their head's regard (the deed "envoyHonored", or
    /// "slighted"), and a very good stay leaves a debt of honour. Hurt while here costs a little regard while the colony
    /// sheltered them, a lot in the open; hurt by the colony, they leave at once. Kept with KeepFor.Envoy while here.
    /// </summary>
    public static class Envoys
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The setting "Envoys": off, nothing new is agreed, and a stay under way runs its season.</summary>
        public static bool On => ColonyCouncilMod.Settings?.diplomacyEnvoys ?? true;

        // ================= Reading =================

        /// <summary>Every stay agreed or under way, with its home (a new list).</summary>
        public static List<(DiplomacyHome home, EnvoyStay stay)> All()
        {
            var list = new List<(DiplomacyHome, EnvoyStay)>();
            var book = DiplomacyBook.Instance;
            if (book == null) return list;
            foreach (var h in book.Live())
                if (h.envoy != null)
                    list.Add((h, h.envoy));
            return list;
        }

        /// <summary>The stay at this home, or null.</summary>
        public static EnvoyStay At(Map home) => DiplomacyBook.Instance?.Existing(home)?.envoy;

        /// <summary>The stay this pawn is part of (the envoy or their attendant), or null.</summary>
        public static EnvoyStay Of(Pawn p, out DiplomacyHome home)
        {
            home = null;
            if (p == null) return null;
            foreach (var (h, s) in All())
                if (s.envoy == p || s.attendant == p)
                {
                    home = h;
                    return s;
                }
            return null;
        }

        public static EnvoyStay Of(Pawn p) => Of(p, out _);

        /// <summary>KeepFor.Envoy still holds: they are the envoy or attendant of a stay.</summary>
        public static bool Holds(Pawn p) => Of(p) != null;

        /// <summary>The stay with this people at any home, or null.</summary>
        public static EnvoyStay With(Faction f, out DiplomacyHome home)
        {
            home = null;
            foreach (var (h, s) in All())
                if (s.people == f)
                {
                    home = h;
                    return s;
                }
            return null;
        }

        /// <summary>Their envoy the colony knows, free to come (out in the world, not held or on the road): of the town nearest the home first.</summary>
        public static Notable EnvoyOf(Faction f, Map home)
        {
            var book = NotableBook.Instance;
            if (f == null || book == null) return null;
            var town = home != null ? WorldSituationWorker.NearestTown(f, home) : null;
            return book.notables.Where(n => n != null && n.faction == f && n.role == NotableRole.Envoy && n.Alive && Free(n.pawn, f))
                .OrderBy(n => town != null && n.town == town.ID ? 0 : 1).FirstOrDefault();
        }

        private static bool Free(Pawn p, Faction f) =>
            p != null && !p.Dead && !p.Discarded && !p.Spawned && p.Faction == f && !p.IsPrisoner && p.GetCaravan() == null && !People.Kidnapped(p)
            && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(p) && !p.DevelopmentalStage.Juvenile();

        public static bool Empire(EnvoyStay s) => Ages.IsEmpire(s?.people);

        /// <summary>The days stayed so far (0 before they arrive).</summary>
        public static float DaysStayed(EnvoyStay s) => s == null || s.since < 0 ? 0f : (Now - s.since) / (float)EnvoyMath.DayTicks;

        /// <summary>How the stay goes, in a word.</summary>
        public static EnvoyMood Mood(EnvoyStay s) => EnvoyMath.Mood(s?.score ?? 0f, DaysStayed(s));

        // ================= Why it can be asked =================

        /// <summary>Why the colony can't ask this people for their envoy now (by letter), or null.</summary>
        public static string AskWhyNot(Faction f, Map home)
        {
            if (!On) return RansomWords.P("CC_Envoy_Why_Off");
            if (f == null || !Diplomacy.Deals(f)) return DipWords.P("CC_Dip_Why_Gone");
            if (Diplomacy.Hostile(f) || !f.HasGoodwill || f.PlayerGoodwill < EnvoyMath.MinGoodwill)
                return RansomWords.P("CC_Envoy_Why_Goodwill", RansomWords.People(f).Named("PEOPLE"), EnvoyMath.MinGoodwill.Named("GOODWILL"));
            var h = DiplomacyBook.Instance?.Existing(home);
            if (h?.envoy != null) return RansomWords.P("CC_Envoy_Why_OneHere", RansomWords.People(h.envoy.people).Named("PEOPLE"));
            if (EnvoyOf(f, home) == null) return RansomWords.P("CC_Envoy_Why_NoEnvoy", RansomWords.People(f).Named("PEOPLE"));
            return null;
        }

        // ================= Their offer =================

        /// <summary>When this people last sent or offered an envoy (the history's dip:envoy with them as the ones who did), or -1.</summary>
        private static int LastOf(Faction f)
        {
            var r = Refs.Of(f);
            foreach (var e in History.Of(r, new[] { DiplomacyMath.Envoy }, Now - (int)(EnvoyMath.PeopleGapDays * EnvoyMath.DayTicks)))
                if (e.actors.Any(a => a.who == r && a.role == HistoryRole.Did))
                    return e.tick;
            return -1;
        }

        /// <summary>Once a day (Diplomacy's day): a people that may now and then offers their envoy to a home, by their letter.</summary>
        public static void Daily(int now)
        {
            if (!On || !Tell.CanAsk) return;
            var book = DiplomacyBook.Instance;
            if (book == null) return;
            foreach (var home in Homes.All)
            {
                if (home == null || MapFacts.InVacuum(home)) continue;
                var h = book.HomeOf(home);
                if (h.envoy != null) continue;
                foreach (var f in Find.FactionManager.AllFactionsListForReading.InRandomOrder())
                {
                    if (f == null || f.IsPlayer || !Diplomacy.Deals(f) || !f.HasGoodwill) continue;
                    int last = LastOf(f);
                    if (h.envoyOfferPeople == f.loadID && h.envoyOfferAt > last) last = h.envoyOfferAt;
                    if (!EnvoyMath.MayOffer(Diplomacy.Hostile(f), f.PlayerGoodwill, EnvoyOf(f, home) != null, false, last, h.envoyOfferAt, now)) continue;
                    if (!EnvoyMath.OffersToday(Rand.Value)) continue;
                    Offer(h, home, f);
                    return;
                }
            }
        }

        /// <summary>Their letter offering their envoy for a season: accept (they come in a day or two), decline, or put it off.</summary>
        public static void Offer(DiplomacyHome h, Map home, Faction f)
        {
            var def = RansomDefOf.Rimfolk_EnvoyOffer;
            var n = EnvoyOf(f, home);
            if (def == null || n == null || h == null) return;
            h.envoyOfferAt = Now;
            h.envoyOfferPeople = f.loadID;
            var head = FavourCalls.HeadOf(f, home);
            var form = RansomWords.EnvoyForm(home, f);
            string who = head != null ? NotableBook.Called(head) : RansomWords.People(f);
            string text = RansomWords.T("CC_Envoy_Offer", who.Named("WHO"), RansomWords.People(f).Named("PEOPLE"), NotableBook.Called(n).Named("ENVOY"), form.word.Named("WORD"),
                              form.rite.Named("RITE"))
                          + "\n\n" + RansomWords.P(Ages.IsEmpire(f) ? "CC_Envoy_Offer_HowEmpire" : "CC_Envoy_Offer_How", n.Name.Named("ENVOY"));
            var town = NotableBook.TownOf(n.town);
            var letter = LetterMaker.MakeLetter(RansomWords.P("CC_Envoy_Offer_Label", RansomWords.People(f).Named("PEOPLE")), text, def,
                town != null ? new LookTargets(town) : LookTargets.Invalid, f);
            if (letter is ChoiceLetter_EnvoyOffer l)
            {
                l.people = f;
                l.home = Homes.KeyOf(home);
                l.notable = n.id;
            }
            Tell.Ask(letter, home);
            Diplomacy.Dirty();
        }

        /// <summary>
        /// A stay agreed (their offer accepted, or the colony's letter answered yes): the envoy is due one to two days on.
        /// Returns the record, or null when one is under way at that home already or their envoy can't come.
        /// </summary>
        public static EnvoyStay Agree(Map home, Faction f, Notable n, bool asked, float days)
        {
            var book = DiplomacyBook.Instance;
            if (book == null || home == null || f == null) return null;
            var h = book.HomeOf(home);
            n ??= EnvoyOf(f, home);
            if (h.envoy != null || n?.pawn == null || !Free(n.pawn, f)) return null;
            h.envoy = new EnvoyStay
            {
                envoy = n.pawn, people = f, stage = EnvoyStage.Coming, agreed = Now, due = Now + Mathf.RoundToInt(Mathf.Max(0.05f, days) * EnvoyMath.DayTicks), asked = asked,
                envoyName = n.pawn.LabelShort
            };
            WorldLedger.Keep(n.pawn, KeepFor.Envoy);
            Diplomacy.Dirty();
            return h.envoy;
        }

        /// <summary>The colony asked by letter for their envoy: they send them by their head's regard. Returns their words.</summary>
        public static string HearAsk(Faction f, Map home, int regard, float days, out bool yes)
        {
            yes = false;
            if (AskWhyNot(f, home) != null) return RansomWords.T("CC_Envoy_Ans_Moot", RansomWords.People(f).Named("PEOPLE"));
            var n = EnvoyOf(f, home);
            var form = RansomWords.EnvoyForm(home, f);
            if (!EnvoyMath.SendsWhenAsked(Diplomacy.Hostile(f), f.PlayerGoodwill, regard, Rand.Value))
                return RansomWords.T("CC_Envoy_Ans_No", RansomWords.People(f).Named("PEOPLE"), NotableBook.Called(n).Named("ENVOY"));
            if (Agree(home, f, n, true, days) == null) return RansomWords.T("CC_Envoy_Ans_Moot", RansomWords.People(f).Named("PEOPLE"));
            yes = true;
            return RansomWords.T("CC_Envoy_Ans_Yes", RansomWords.People(f).Named("PEOPLE"), NotableBook.Called(n).Named("ENVOY"), form.rite.Named("RITE"));
        }

        // ================= The hour =================

        /// <summary>The hour (Diplomacy.Hourly): each stay a step on.</summary>
        public static void Hourly(int now)
        {
            foreach (var (h, s) in All())
            {
                try
                {
                    Step(h, s, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("envoys: the hour", e);
                }
            }
        }

        private static void Step(DiplomacyHome h, EnvoyStay s, int now)
        {
            var map = h.Map;
            var f = s.people;
            if (map == null || f == null || f.defeated)
            {
                Clear(h, s);
                return;
            }
            switch (s.stage)
            {
                case EnvoyStage.Coming:
                    if (s.envoy == null || s.envoy.Dead || s.envoy.Discarded || Diplomacy.Hostile(f))
                    {
                        Clear(h, s);
                        // apart: the stay agreed won't come; told in a message.
                        Tell.Message(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Envoy_Msg_NotComing", RansomWords.People(f).Named("PEOPLE")), LookTargets.Invalid,
                            MessageTypeDefOf.NeutralEvent);
                        return;
                    }
                    if (LordOf(map, s) != null) return;
                    if (now < s.due || MapFacts.ThreatOn(map)) return;
                    if (now - s.due > EnvoyMath.StayDays * EnvoyMath.DayTicks)
                    {
                        Clear(h, s);
                        return;
                    }
                    Arrive(h, s, map);
                    return;
                case EnvoyStage.Staying:
                    Stay(h, s, map, now);
                    return;
                default:
                    Clear(h, s);
                    return;
            }
        }

        /// <summary>The stay's group on the home, or null.</summary>
        public static Lord LordOf(Map map, EnvoyStay s)
        {
            if (map?.lordManager == null || s == null) return null;
            foreach (var l in map.lordManager.lords)
                if (l?.LordJob is LordJob_EnvoyStay && (s.envoy != null && l.ownedPawns.Contains(s.envoy) || s.attendant != null && l.ownedPawns.Contains(s.attendant)))
                    return l;
            return null;
        }

        /// <summary>The envoy and an attendant walk in from the edge toward the colony's common rooms, with food enough for the season.</summary>
        private static void Arrive(DiplomacyHome h, EnvoyStay s, Map map)
        {
            var f = s.people;
            if (!Arrivals.TryEntryCell(map, out var entry)) return;
            var party = new List<Pawn>();
            try
            {
                if (!Arrivals.Spawn(s.envoy, map, entry)) return;
                party.Add(s.envoy);
                var kind = GuardKind(f) ?? Arrivals.PersonKind(f);
                if (kind != null)
                {
                    var a = Arrivals.Make(kind, f);
                    GenSpawn.Spawn(a, CellFinder.RandomClosewalkCellNear(entry, map, 3), map);
                    party.Add(a);
                    s.attendant = a;
                    WorldLedger.Keep(a, KeepFor.Envoy, false);
                }
                foreach (var p in party) Hosting.Provision(p, EnvoyMath.StayDays);
                var spot = Visitors.IndoorsCell(map, party);
                if (!spot.IsValid) spot = DropCellFinder.TradeDropSpot(map);
                LordMaker.MakeNewLord(f, new LordJob_EnvoyStay(Homes.KeyOf(map), spot), map, party);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys: their arrival", e);
                return;
            }
            s.stage = EnvoyStage.Staying;
            s.since = Now;
            s.until = Now + Mathf.RoundToInt(EnvoyMath.StayDays * EnvoyMath.DayTicks);
            s.warmth = Warmth(s.envoy);
            s.gifts = Gifts(s.envoy);
            s.injuries = Injuries(s.envoy);
            Diplomacy.Dirty();
            var form = RansomWords.EnvoyForm(map, f);
            Diplomacy.Write(DiplomacyMath.Envoy, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(WorldLedger.PersonOf(s.envoy), HistoryRole.About), new HistoryActor(Diplomacy.Colony(map), HistoryRole.For)
            }, Diplomacy.Colony(map), RansomWords.P("CC_Envoy_Hist_Came", s.envoy.Named("ENVOY"), RansomWords.People(f).Named("PEOPLE"), form.word.Named("WORD")), true);
            Tell.Letter(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Envoy_Arrives_Label", s.envoy.Named("ENVOY")),
                RansomWords.T("CC_Envoy_Arrives", s.envoy.Named("ENVOY"), RansomWords.People(f).Named("PEOPLE"), form.rite.Named("RITE"), form.word.Named("WORD"))
                + "\n\n" + RansomWords.P(Empire(s) ? "CC_Envoy_Arrives_HowEmpire" : "CC_Envoy_Arrives_How", s.envoy.Named("ENVOY")),
                LetterDefOf.PositiveEvent, new LookTargets(s.envoy), f);
        }

        /// <summary>One of their lighter fighters to attend the envoy, or none.</summary>
        private static PawnKindDef GuardKind(Faction f)
        {
            var opts = f?.def?.pawnGroupMakers?.Where(m => m?.kindDef == PawnGroupKindDefOf.Combat && m.options != null).SelectMany(m => m.options)
                .Select(o => o.kind).Where(k => k?.RaceProps?.Humanlike == true && k.combatPower <= 90f).ToList();
            return opts == null || opts.Count == 0 ? null : opts.RandomElement();
        }

        // ================= The stay =================

        private static IEnumerable<Pawn> Party(EnvoyStay s)
        {
            if (s.envoy != null) yield return s.envoy;
            if (s.attendant != null) yield return s.attendant;
        }

        private static bool HereOn(Pawn p, Map map) => p != null && !p.Dead && p.Spawned && p.Map == map;

        /// <summary>
        /// The hour while they stay: the season's end; the day's tally; hurt seen; meals and beds by the hosting rules; a
        /// seat at a council meeting; their runner for a letter home.
        /// </summary>
        private static void Stay(DiplomacyHome h, EnvoyStay s, Map map, int now)
        {
            var f = s.people;
            var e = s.envoy;
            // Downed, they left the stay's group (the game's own rule), so a death or an arrest after that never reached
            // it (LordJob_EnvoyStay.Notify_PawnLost): seen here instead. Dead, as a death at the colony; taken prisoner, as
            // an arrest.
            if (e != null && e.Dead)
            {
                Died(h, s, map, e);
                return;
            }
            if (e != null && (e.IsPrisoner || e.Faction != f))
            {
                End(h, s, map, true);
                return;
            }
            if (e == null || e.MapHeld != map)
            {
                // Gone from the map without the season's end (left in a hurry): the stay ends as it stands.
                End(h, s, map, false);
                return;
            }
            if (now >= s.until || Diplomacy.Hostile(f))
            {
                End(h, s, map, false);
                return;
            }
            // Carried (a colonist taking them to a bed after they were downed): still here; the hour waits until they're set down.
            if (!e.Spawned) return;
            Regroup(s, map);
            Tally(s, map);
            int hurt = Injuries(s.envoy);
            if (hurt > s.injuries)
            {
                bool sheltered = !(s.envoy.GetRoom()?.PsychologicallyOutdoors ?? true);
                s.score += EnvoyMath.Hurt(sheltered);
                // apart: what their envoy will say of being hurt at the colony, told once it is seen.
                Tell.Minor(NewsKind.World, Importance.Notable, map, RansomWords.P(sheltered ? "CC_Envoy_Msg_HurtInside" : "CC_Envoy_Msg_HurtOutside", s.envoy.Named("ENVOY")),
                    new LookTargets(s.envoy));
            }
            s.injuries = hurt;
            Tend(s, map);
            SitIn(s, map);
            Letters(h, s, map, now);
        }

        /// <summary>One of them up again after being downed or out of their mind, with no group: back to the stay's (or a new one at the common rooms).</summary>
        private static void Regroup(EnvoyStay s, Map map)
        {
            var lost = Party(s).Where(p => HereOn(p, map) && !p.Downed && !p.InMentalState && p.GetLord() == null && !p.IsPrisoner && p.Faction == s.people).ToList();
            if (lost.Count == 0) return;
            try
            {
                var lord = LordOf(map, s);
                if (lord != null)
                {
                    foreach (var p in lost)
                        if (lord.CanAddPawn(p))
                            lord.AddPawn(p);
                    return;
                }
                var spot = Visitors.IndoorsCell(map, lost);
                if (!spot.IsValid) spot = DropCellFinder.TradeDropSpot(map);
                LordMaker.MakeNewLord(s.people, new LordJob_EnvoyStay(Homes.KeyOf(map), spot), map, lost);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys: back to the stay", e);
            }
        }

        /// <summary>The day just ended, tallied once the day turns: meals, their talks with the colonists, gifts.</summary>
        private static void Tally(EnvoyStay s, Map map)
        {
            int today = GenLocalDate.DayOfYear(map) + GenLocalDate.Year(map) * GenDate.DaysPerYear;
            if (s.day == today) return;
            if (s.day >= 0)
            {
                bool empire = Empire(s);
                s.score += EnvoyMath.Day(s.meals > 0, s.packs && s.meals == 0, empire, s.fine > 0);
                float w = Warmth(s.envoy);
                s.score += EnvoyMath.Talks(w - s.warmth);
                s.warmth = w;
                int g = Gifts(s.envoy);
                if (g > s.gifts) s.score += (g - s.gifts) * EnvoyMath.Gift;
                s.gifts = Mathf.Max(s.gifts, g);
            }
            s.day = today;
            s.meals = s.fine = 0;
            s.packs = false;
        }

        /// <summary>The bond between the envoy and the colonists they talked with (the outsiders' own record).</summary>
        private static float Warmth(Pawn envoy)
        {
            var book = OutsiderBook.Instance;
            if (book == null || envoy == null) return 0f;
            float sum = 0f;
            foreach (var p in book.pairs)
                if (p != null && p.outsider == envoy)
                    sum += p.warmth;
            return sum;
        }

        private static int Gifts(Pawn envoy)
        {
            var book = OutsiderBook.Instance;
            if (book == null || envoy == null) return 0;
            int n = 0;
            foreach (var v in book.visits)
                if (v != null && v.outsider == envoy)
                    n += v.gifts;
            return n;
        }

        private static int Injuries(Pawn p) => p?.health?.hediffSet?.hediffs?.Count(x => x is Hediff_Injury) ?? 0;

        /// <summary>Free to be sent to eat, to bed, to the meeting or the gate: here for the stay, up, calm, not busy with something of their own, no raid on.</summary>
        public static bool Free(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned || p.Downed || p.InMentalState || p.Drafted || !p.Awake() || p.InBed()) return false;
            if (!(p.GetLord()?.CurLordToil is LordToil_EnvoyStay)) return false;
            var d = p.CurJobDef;
            if (d == RansomDefOf.Rimfolk_EnvoyLetter || d == RansomDefOf.Rimfolk_EnvoySitIn || d == JobDefOf.Ingest) return false;
            return PlayOuts.Interruptible(p) && p.Map.dangerWatcher.DangerRating != StoryDanger.High;
        }

        /// <summary>
        /// Meals and beds, by the missions' hosting rules (Missions/Hosting.cs): the hungry go and eat the colony's food at its
        /// table (fine meals first for a guest of rank), anyone off to bed hungry has supper first, with nothing of the
        /// colony's they eat from their packs; at night each lies down in the bed set aside (or a spare one, or the floor),
        /// and once they've slept they get up.
        /// </summary>
        private static void Tend(EnvoyStay s, Map map)
        {
            foreach (var p in Party(s).ToList())
            {
                if (!HereOn(p, map))
                {
                    Hosting.GiveBack(p);
                    continue;
                }
                bool envoy = p == s.envoy;
                float food = p.needs?.food?.CurLevelPercentage ?? 1f;
                bool sleepy = Hosting.WantsSleep(p);
                if (Hosting.LyingAwake(p) && !sleepy)
                {
                    if (food < Hosting.EatBelow && Eat(s, p, envoy)) continue;
                    Hosting.GetUp(p);
                    continue;
                }
                if (!Free(p)) continue;
                if (food < Hosting.EatBelow || sleepy && food < Hosting.SupperBelow)
                {
                    if (Eat(s, p, envoy)) continue;
                    if (food < Hosting.PackBelow || sleepy)
                    {
                        var pack = Hosting.Pack(p);
                        if (pack != null && Hosting.Eat(p, pack, null))
                        {
                            if (envoy) s.packs = true;
                            continue;
                        }
                    }
                }
                if (!sleepy) continue;
                var bed = BedFor(s, p, map);
                if (bed != null && Hosting.Sleep(p, bed))
                {
                    if (envoy) Night(s, map, bed == s.bed ? EnvoyNight.Own : EnvoyNight.Spare, bed);
                    continue;
                }
                if (Hosting.SleepRough(p) && envoy) Night(s, map, EnvoyNight.Floor, null);
            }
        }

        /// <summary>Something of the colony's to eat, started: counted for the day (a fine meal for the Empire's envoy).</summary>
        private static bool Eat(EnvoyStay s, Pawn p, bool envoy)
        {
            var meal = Hosting.Meal(p, envoy) ?? (envoy ? null : Hosting.Raw(p));
            if (meal == null) return false;
            bool fine = meal.def.ingestible != null && meal.def.ingestible.preferability >= FoodPreferability.MealFine;
            if (!Hosting.Eat(p, meal, null)) return false;
            if (!envoy) return true;
            try
            {
                p.jobs.curDriver?.AddFinishAction(c =>
                {
                    if (c != JobCondition.Succeeded) return;
                    s.meals++;
                    if (fine) s.fine++;
                });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys: a meal counted", e);
            }
            return true;
        }

        /// <summary>The bed set aside for them while it is usable, else a spare one of the colony's they'd share, else none.</summary>
        private static Building_Bed BedFor(EnvoyStay s, Pawn p, Map map)
        {
            bool Ours(Pawn o) => o == s.envoy || o == s.attendant;
            if (p == s.envoy && s.bed != null && s.bed.Spawned && s.bed.Map == map && BedUsable(s.bed, s, p)) return s.bed;
            return Hosting.Beds(map, Ours, p).Where(b => b != s.bed || p == s.envoy).OrderBy(b => b.Position.DistanceToSquared(p.Position))
                .FirstOrDefault(b => Hosting.CanShare(b, p) && (b.OwnersForReading.Contains(p) || b.OwnersForReading.Count < b.SleepingSlotsCount));
        }

        /// <summary>A bed that can be set aside for the envoy: the colony's, for people, not a prisoner's, a slave's or a sickbed, nobody's but theirs (a guest bed of Hospitality's too).</summary>
        public static bool BedUsable(Building_Bed bed, EnvoyStay s, Pawn p)
        {
            if (bed == null || !bed.Spawned || bed.Medical || bed.ForPrisoners || bed.ForSlaves || bed.Faction != Faction.OfPlayer || bed.IsBurning()) return false;
            if (bed.def.building?.bed_humanlike != true || p != null && !RestUtility.CanUseBedEver(p, bed.def)) return false;
            foreach (var o in bed.OwnersForReading)
                if (o != null && o != s?.envoy && o != s?.attendant)
                    return false;
            return true;
        }

        /// <summary>The beds the player may set aside for their envoy, nearest the common rooms first.</summary>
        public static List<Building_Bed> BedChoices(EnvoyStay s, Map map)
        {
            var list = new List<Building_Bed>();
            if (map == null) return list;
            foreach (var bed in map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>())
            {
                var room = bed.GetRoom();
                if (room == null || room.IsPrisonCell || room.PsychologicallyOutdoors) continue;
                if (BedUsable(bed, s, s?.envoy)) list.Add(bed);
            }
            return list;
        }

        /// <summary>A night counted once: the bed of their own, a spare one, the floor; for the Empire's envoy, whether the room was theirs alone.</summary>
        private static void Night(EnvoyStay s, Map map, EnvoyNight n, Building_Bed bed)
        {
            int day = GenLocalDate.DayOfYear(map) + GenLocalDate.Year(map) * GenDate.DaysPerYear;
            int night = GenLocalDate.HourOfDay(map) < 12 ? day - 1 : day;
            if (s.nightDay == night) return;
            s.nightDay = night;
            s.night = n;
            var room = bed?.GetRoom();
            bool alone = room != null && !room.PsychologicallyOutdoors && room.ContainedBeds.Count() == 1;
            s.score += EnvoyMath.Night(n, Empire(s), alone);
        }

        /// <summary>A council meeting in session at the home: the envoy walks over and sits in (once a meeting).</summary>
        private static void SitIn(EnvoyStay s, Map map)
        {
            var m = AmbitionTracker.MeetingOn(map);
            var def = RansomDefOf.Rimfolk_EnvoySitIn;
            if (m == null || def == null || !m.opened || m.startedTick < 0 || !m.spot.IsValid || s.satIn == m.calledTick || !Free(s.envoy)) return;
            if (!CellFinder.TryFindRandomCellNear(m.spot, map, 5, c => c.Standable(map) && c.GetFirstPawn(map) == null && s.envoy.CanReach(c, PathEndMode.OnCell, Danger.Some), out var cell))
                return;
            var job = JobMaker.MakeJob(def, cell, m.spot);
            job.expiryInterval = 2 * GenDate.TicksPerHour;
            s.envoy.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (s.envoy.CurJob != job) return;
            s.satIn = m.calledTick;
            s.score += EnvoyMath.SatIn;
            Diplomacy.Dirty();
            // apart: told as the envoy walks over to the meeting; they sit in on the map.
            Tell.Message(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Envoy_Msg_SitIn", s.envoy.Named("ENVOY")), new LookTargets(s.envoy), MessageTypeDefOf.NeutralEvent);
        }

        // ================= Letters home =================

        /// <summary>Their runner's group for this home, or null.</summary>
        public static Lord RunnerOf(Map map)
        {
            if (map?.lordManager == null) return null;
            int key = Homes.KeyOf(map);
            foreach (var l in map.lordManager.lords)
                if (l?.LordJob is LordJob_Messenger m && m.offer < 0 && m.envoyHome == key)
                    return l;
            return null;
        }

        /// <summary>
        /// Every five days of the stay their runner comes to the gate, and once the envoy is free they walk out to hand over
        /// their letter home.
        /// </summary>
        private static void Letters(DiplomacyHome h, EnvoyStay s, Map map, int now)
        {
            var runner = RunnerOf(map);
            if (runner == null)
            {
                if (!EnvoyMath.LetterDue(DaysStayed(s), s.letters) || MapFacts.ThreatOn(map) || !Arrivals.TryEntryCell(map, out var entry)) return;
                var by = Ransoms.Bearer(s.people);
                if (by == null) return;
                if (Ransoms.SendMessenger(-1, by, map, entry, EnvoyMath.RunnerWaitHours, Homes.KeyOf(map)) != null) s.letters++;
                return;
            }
            var job = runner.LordJob as LordJob_Messenger;
            var r = Ransoms.Messenger(runner);
            var def = RansomDefOf.Rimfolk_EnvoyLetter;
            if (job == null || job.arrived < 0 || job.done || r == null || def == null || !Free(s.envoy)) return;
            var go = JobMaker.MakeJob(def, r);
            // together: the envoy walks out to their runner at the gate and hands the letter over face to face.
            s.envoy.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>Their runner reached the gate (LordJob_Messenger): the envoy walks out once free (the hour sends them).</summary>
        public static void RunnerArrived(LordJob_Messenger job)
        {
        }

        /// <summary>The letter handed over (JobDriver_EnvoyLetter): their head's regard moves by how the stay goes, and the runner goes home.</summary>
        public static void LetterHanded(Pawn envoy, Pawn runner)
        {
            var s = Of(envoy, out var h);
            var map = h?.Map;
            if (s == null || map == null) return;
            if (runner?.GetLord()?.LordJob is LordJob_Messenger m) m.done = true;
            var mood = Mood(s);
            var head = FavourCalls.HeadOf(s.people, map);
            int shift = EnvoyMath.LetterRegard(mood);
            if (head != null && shift != 0)
            {
                head.personal += shift;
                head.Remember(RansomWords.P("CC_Envoy_Note_Letter", envoy.Named("ENVOY"), RansomWords.Mood(mood).Named("MOOD")), shift);
            }
            Diplomacy.Dirty();
            // apart: news of the letter home, told in the evening's news; it was handed over at the gate on the map.
            Tell.Minor(NewsKind.World, Importance.Flavor, map, RansomWords.T("CC_Envoy_Msg_Letter_" + mood, envoy.Named("ENVOY"), RansomWords.People(s.people).Named("PEOPLE")),
                new LookTargets(envoy));
        }

        // ================= Acts =================

        /// <summary>Why a bed can't be set aside for them now, or null.</summary>
        public static string BedWhyNot(EnvoyStay s, Map map)
        {
            if (s == null || s.stage != EnvoyStage.Staying) return RansomWords.P("CC_Envoy_Why_NotHere");
            return BedChoices(s, map).Count == 0 ? RansomWords.P("CC_Envoy_Why_NoBed") : null;
        }

        /// <summary>The player picks the bed set aside for the envoy (a menu of the colony's free beds).</summary>
        public static void ChooseBed(EnvoyStay s, Map map)
        {
            var opts = new List<FloatMenuOption>();
            foreach (var bed in BedChoices(s, map).OrderBy(b => b.GetRoom()?.ContainedBeds.Count() ?? 9).Take(20))
            {
                var b = bed;
                var room = b.GetRoom();
                string label = RansomWords.P(room != null && room.ContainedBeds.Count() == 1 ? "CC_Envoy_Bed_Alone" : "CC_Envoy_Bed_Shared", b.LabelCap.Named("BED"),
                    (room?.GetRoomRoleLabel() ?? "").Named("ROOM"));
                opts.Add(new FloatMenuOption(label, () =>
                {
                    s.bed = b;
                    Diplomacy.Dirty();
                    // apart: the player's choice, confirmed in a message; the envoy sleeps there from tonight on the map.
                    Tell.Message(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Envoy_Msg_BedSet", (s.envoy?.LabelShort ?? s.envoyName ?? "?").Named("ENVOY"),
                        b.LabelCap.Named("BED")), new LookTargets(b), MessageTypeDefOf.NeutralEvent);
                }));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption(RansomWords.P("CC_Envoy_Why_NoBed"), null));
            Find.WindowStack.Add(new FloatMenu(opts, RansomWords.P("CC_Envoy_Bed_Title", (s.envoy?.LabelShort ?? s.envoyName ?? "?").Named("ENVOY"))));
        }

        /// <summary>The colony sees their envoy off before the season is out: a slight, said as such.</summary>
        public static void SeeOff(EnvoyStay s)
        {
            var e = s?.envoy;
            Of(e, out var h);
            var map = h?.Map;
            if (s == null || map == null || s.stage != EnvoyStage.Staying) return;
            s.score = Mathf.Min(s.score, -1f * Mathf.Max(1f, DaysStayed(s)));
            End(h, s, map, true);
        }

        // ================= The end =================

        /// <summary>
        /// The season is over (or cut short): the envoy and attendant walk out, the bed goes back, and their report moves their
        /// head's regard; content, the deed "envoyHonored" (a very good stay, big enough for a debt of honour); offended or
        /// seen off, "slighted".
        /// </summary>
        public static void End(DiplomacyHome h, EnvoyStay s, Map map, bool seenOff)
        {
            var f = s.people;
            s.stage = EnvoyStage.Leaving;
            try
            {
                var lord = LordOf(map, s);
                lord?.ReceiveMemo(LordJob_EnvoyStay.MemoLeave);
                foreach (var p in Party(s).ToList())
                {
                    // One the colony took prisoner keeps the bed the wardens gave them.
                    if (p.IsPrisonerOfColony) continue;
                    Hosting.GetUp(p);
                    Hosting.GiveBack(p, map);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("envoys: seeing them off", e);
            }
            float days = DaysStayed(s);
            var mood = seenOff ? EnvoyMood.Offended : EnvoyMath.Mood(s.score, days);
            if (f != null && !f.defeated && s.envoy != null && !s.envoy.Dead)
            {
                var head = FavourCalls.HeadOf(f, map);
                int shift = EnvoyMath.EndRegard(mood);
                string name = s.envoy.LabelShort;
                if (head != null)
                {
                    head.personal += shift;
                    head.Remember(RansomWords.P("CC_Envoy_Note_End", name.Named("ENVOY"), RansomWords.Mood(mood).Named("MOOD")), shift);
                }
                var n = NotableBook.Instance?.notables.FirstOrDefault(x => x != null && x.pawn == s.envoy);
                if (n != null) n.Remember(RansomWords.P("CC_Envoy_Note_Stayed", RansomWords.Mood(mood).Named("MOOD")), mood == EnvoyMood.Content ? 6f : mood == EnvoyMood.Offended ? -6f : 1f);
                if (mood == EnvoyMood.Content) FactionMemory.Note(f, "envoyHonored", name, EnvoyMath.DeedWeight(s.score, days));
                else if (mood == EnvoyMood.Offended) FactionMemory.Note(f, "slighted", name);
                var form = RansomWords.EnvoyForm(map, f);
                Diplomacy.Write(DiplomacyMath.Envoy, new List<HistoryActor>
                {
                    new HistoryActor(WorldLedger.PersonOf(s.envoy), HistoryRole.About), new HistoryActor(Diplomacy.Colony(map), HistoryRole.With), new HistoryActor(Refs.Of(f), HistoryRole.For)
                }, Diplomacy.Colony(map), RansomWords.P("CC_Envoy_Hist_Left_" + mood, name.Named("ENVOY"), RansomWords.People(f).Named("PEOPLE"), form.word.Named("WORD")),
                    mood != EnvoyMood.Cool);
                Tell.Letter(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Envoy_Leaves_Label", name.Named("ENVOY")),
                    RansomWords.T(seenOff ? "CC_Envoy_Leaves_SeenOff" : "CC_Envoy_Leaves_" + mood, name.Named("ENVOY"), RansomWords.People(f).Named("PEOPLE"), form.word.Named("WORD")),
                    mood == EnvoyMood.Content ? LetterDefOf.PositiveEvent : mood == EnvoyMood.Offended ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent,
                    s.envoy.Spawned ? new LookTargets(s.envoy) : LookTargets.Invalid, f);
            }
            Clear(h, s);
        }

        /// <summary>
        /// Their envoy is lost to the stay (LordJob_EnvoyStay): dead at the colony costs their head's regard, a little while
        /// the colony sheltered them and a lot in the open; taken (arrested, carried off) the stay ends.
        /// </summary>
        public static void Lost(int homeKey, Pawn p, PawnLostCondition condition)
        {
            var s = Of(p, out var h);
            var map = h?.Map;
            if (s == null || map == null || p != s.envoy || s.stage != EnvoyStage.Staying) return;
            if (!p.Dead)
            {
                // Taken by the colony (arrested, made one of its own): the stay ends as an insult. Downed, or out of their
                // mind a while: they rejoin the stay once up again (Regroup).
                if (condition == PawnLostCondition.MadePrisoner || condition == PawnLostCondition.ChangedFaction) End(h, s, map, true);
                return;
            }
            Died(h, s, map, p);
        }

        /// <summary>The envoy died at the colony (in the stay's group, or downed out of it before): regard by shelter, the letter, the stay over.</summary>
        private static void Died(DiplomacyHome h, EnvoyStay s, Map map, Pawn p)
        {
            var f = s.people;
            var room = p.Corpse?.GetRoom();
            bool sheltered = room != null && !room.PsychologicallyOutdoors;
            var head = FavourCalls.HeadOf(f, map);
            int shift = EnvoyMath.DiedRegard(sheltered);
            if (head != null)
            {
                head.personal += shift;
                head.Remember(RansomWords.P("CC_Envoy_Note_Died", p.Named("ENVOY")), shift);
            }
            FactionMemory.Note(f, "slighted", p.LabelShort, sheltered ? 4 : 12);
            var lord = LordOf(map, s);
            lord?.ReceiveMemo(LordJob_EnvoyStay.MemoLeave);
            if (s.attendant != null) Hosting.GiveBack(s.attendant, map);
            Hosting.GiveBack(p, map);
            Diplomacy.Write(DiplomacyMath.Envoy, new List<HistoryActor>
            {
                new HistoryActor(WorldLedger.PersonOf(p), HistoryRole.About), new HistoryActor(Diplomacy.Colony(map), HistoryRole.With), new HistoryActor(Refs.Of(f), HistoryRole.For)
            }, Diplomacy.Colony(map), RansomWords.P("CC_Envoy_Hist_Died", p.Named("ENVOY"), RansomWords.People(f).Named("PEOPLE")), true);
            Tell.Letter(NewsKind.World, Importance.Important, map, RansomWords.P("CC_Envoy_Died_Label", p.Named("ENVOY")),
                RansomWords.T(sheltered ? "CC_Envoy_Died_Inside" : "CC_Envoy_Died_Outside", p.Named("ENVOY"), RansomWords.People(f).Named("PEOPLE")), LetterDefOf.NegativeEvent,
                p.Corpse != null && p.Corpse.Spawned ? new LookTargets(p.Corpse) : LookTargets.Invalid, f);
            Clear(h, s);
        }

        /// <summary>Hurt by the colony while its guest: they leave at once (the lord runs), and the stay ends as an insult.</summary>
        public static void Harmed(int homeKey)
        {
            var map = Homes.MapOf(homeKey);
            var h = DiplomacyBook.Instance?.Existing(map);
            var s = h?.envoy;
            if (s == null || s.stage != EnvoyStage.Staying) return;
            s.score = Mathf.Min(s.score, -1f * Mathf.Max(1f, DaysStayed(s)) + EnvoyMath.HurtOpen);
            End(h, s, map, true);
        }

        /// <summary>The record let go: the keep reasons and the bed.</summary>
        private static void Clear(DiplomacyHome h, EnvoyStay s)
        {
            if (h == null) return;
            if (h.envoy == s) h.envoy = null;
            // Let go of the envoy's reason; a notable stays kept as one (the ledger's other reason holds them).
            foreach (var p in Party(s).ToList())
                if (WorldLedger.Release(p, KeepFor.Envoy) && !p.Spawned && !p.Dead)
                    Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            Diplomacy.Dirty();
        }

        /// <summary>A home is no longer the colony's: a stay there ends (they go home).</summary>
        internal static void HomeLost(DiplomacyHome h)
        {
            if (h?.envoy == null) return;
            var map = h.Map;
            LordOf(map, h.envoy)?.ReceiveMemo(LordJob_EnvoyStay.MemoLeave);
            Clear(h, h.envoy);
        }

        // ================= Words =================

        /// <summary>Why they are here, for Here and away ("staying the season as the colony's guest"), or null for anyone else.</summary>
        public static string HereAs(Pawn p)
        {
            var s = Of(p);
            if (s == null || s.stage != EnvoyStage.Staying) return null;
            var form = RansomWords.EnvoyForm(Homes.Seat, s.people);
            return p == s.envoy ? RansomWords.P("CC_Envoy_HereAs", form.word.Named("WORD"), RansomWords.Mood(Mood(s)).Named("MOOD"))
                : RansomWords.P("CC_Envoy_HereAs_Attendant", (s.envoy?.LabelShort ?? s.envoyName ?? "?").Named("ENVOY"));
        }
    }

    /// <summary>A people at peace offers to send their envoy to live at the colony for a season: accept, decline, or put it off.</summary>
    public class ChoiceLetter_EnvoyOffer : RimfolkLetter
    {
        public Faction people;
        public int home = HomesMath.None;
        public int notable = -1;

        private bool Valid(out Map map, out Notable n)
        {
            map = Homes.MapOf(home) ?? Homes.Seat;
            n = NotableBook.Instance?.Get(notable);
            return people != null && !people.defeated && !Diplomacy.Hostile(people) && map != null && Envoys.At(map) == null && n != null && n.Alive;
        }

        public bool Waiting => !ArchivedOnly && Valid(out _, out _);

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || !Valid(out _, out _))
                {
                    yield return Option_Close;
                    yield break;
                }
                // Each answer is written in the history's answers' log.
                yield return Answers.Recorded(new DiaOption(RansomWords.P("CC_Envoy_Opt_Accept"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        if (!Valid(out var map, out var n)) return;
                        var s = Envoys.Agree(map, people, n, false, EnvoyMath.ComeDays(Rand.Value));
                        // apart: the colony's answer to their letter; their envoy comes later.
                        if (s != null)
                            Tell.Message(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Envoy_Msg_Accepted", NotableBook.Called(n).Named("ENVOY"),
                                RansomWords.People(people).Named("PEOPLE")), LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                    },
                    resolveTree = true
                }, this, "envoy", people, null, () => Homes.MapOf(home) ?? Homes.Seat);
                yield return Answers.Recorded(new DiaOption(RansomWords.P("CC_Envoy_Opt_Decline"))
                {
                    action = () => Find.LetterStack.RemoveLetter(this),
                    resolveTree = true
                }, this, "envoy", people, null, () => Homes.MapOf(home) ?? Homes.Seat);
                yield return Option_Postpone;
            }
        }

        /// <summary>The event card: the people it comes from.</summary>
        public override void Describe(EventCardFacts facts) => facts.faction ??= people;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref notable, "notable", -1);
        }
    }
}
