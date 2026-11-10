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
    // A match's days on the map, from the agreement to the wedding (Matches.cs has the whole of it).
    public static partial class Matches
    {
        // ================= The hour =================

        /// <summary>The hour (Diplomacy.Hourly): each match a step on, by where it stands.</summary>
        public static void Hourly(int now)
        {
            foreach (var (h, p) in All())
            {
                try
                {
                    Step(h, p, h.Map, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("matches: the hour", e);
                }
            }
        }

        private static void Step(DiplomacyHome h, MatchPlan p, Map map, int now)
        {
            if (map == null)
            {
                Off(h, p, null, false);
                return;
            }
            // One of the two lost before the wedding: dead, gone from the world, their people fallen.
            if (p.ours == null || p.ours.Dead || p.ours.Discarded || p.theirs == null || p.theirs.Dead || p.theirs.Discarded || p.people == null || p.people.defeated)
            {
                var gone = p.ours == null || p.ours.Dead || p.ours.Discarded ? p.ours : p.theirs;
                Off(h, p, MatchWords.P("CC_Match_Off_Died", MatchWords.Name(gone, gone == p.ours ? p.oursName : p.theirsName).Named("NAME"),
                    MatchWords.Name(p.ours, p.oursName).Named("OURS"), MatchWords.Name(p.theirs, p.theirsName).Named("THEIRS")), false);
                return;
            }
            // At war with the colony now: no wedding while it lasts.
            if (p.stage != MatchStage.Away && Diplomacy.Hostile(p.people))
            {
                Off(h, p, MatchWords.P("CC_Match_Off_War", MatchWords.People(p.people).Named("PEOPLE"), p.ours.Named("OURS"), p.theirs.Named("THEIRS")), false);
                return;
            }
            // Married already (the game's own ceremony, whoever began it).
            if (p.ours.relations.DirectRelationExists(PawnRelationDefOf.Spouse, p.theirs))
            {
                Wed(h, p, map, true);
                return;
            }
            // The engagement broken by the two of them (the game's own breakup) once it was made.
            if (p.stage != MatchStage.Asking && !p.ours.relations.DirectRelationExists(PawnRelationDefOf.Fiance, p.theirs))
            {
                var who = NotableBook.Instance?.Get(p.notable);
                if (who != null) who.personal += MatchMath.PartedRegard;
                Off(h, p, MatchWords.P("CC_Match_Off_Broken", p.ours.Named("OURS"), p.theirs.Named("THEIRS")), false);
                return;
            }
            switch (p.stage)
            {
                case MatchStage.Asking:
                    MatchObjection.Hour(h, p);
                    return;
                case MatchStage.Agreed:
                    if (p.lives == MatchHome.Colony) Coming(h, p, map, now);
                    else Fetch(h, p, map, now);
                    return;
                case MatchStage.Here:
                    if (p.lives == MatchHome.Colony) Ceremony(h, p, map, now);
                    else EscortHour(h, p, map, now);
                    return;
                case MatchStage.Away:
                    if (p.ours.Spawned) return;
                    // Off the map with their kin: both kept in the world for the wedding there.
                    WorldLedger.Keep(p.ours, KeepFor.Spouse);
                    WorldLedger.Keep(p.theirs, KeepFor.Spouse);
                    if (now - p.left >= MatchMath.RoadDays(TravelDays(p, map)) * MatchMath.DayTicks) WedAway(h, p, map);
                    return;
            }
        }

        // ================= Theirs comes to the colony =================

        /// <summary>
        /// The wedding's guests (Feasts.Host): their notables of the town the match is with (the one who spoke for it first, never the
        /// one to be married), up to two, coming on the day set, to wait at the gate, see the wedding and sit at the evening's feast.
        /// None free to come: the one to be married walks in alone. A feast with guests already under way at the home: tried
        /// again each hour until the day.
        /// </summary>
        private static void Host(DiplomacyHome h, MatchPlan p, Map map)
        {
            if (p.feast >= 0 || p.alone) return;
            var book = NotableBook.Instance;
            var guests = new List<Notable>();
            if (book != null)
                foreach (var n in book.notables.Where(n => n != null && n.faction == p.people && n.Alive && n.pawn != p.theirs && !n.pawn.Spawned && LivingEvents.FreeToTravel(n.pawn))
                             .OrderBy(n => n.id == p.notable ? 0 : n.town == p.town ? 1 : 2).ThenBy(n => n.role == NotableRole.Head ? 0 : 1))
                {
                    if (guests.Count >= 2) break;
                    guests.Add(n);
                }
            if (guests.Count == 0)
            {
                p.alone = true;
                return;
            }
            var g = Feasts.Host(map, p.people, guests, FeastPurpose.Wedding, p.due, MatchMath.FeastTag(p.id));
            if (g != null) p.feast = g.id;
        }

        /// <summary>Their kin on the way: hosted as the wedding's guests, or (nobody to come, or the feast came to nothing) the one to be married walks in alone on a safe hour.</summary>
        private static void Coming(DiplomacyHome h, MatchPlan p, Map map, int now)
        {
            if (now > p.due + MatchMath.GiveUpDays * MatchMath.DayTicks)
            {
                Off(h, p, MatchWords.P("CC_Match_Off_NeverCame", p.theirs.Named("THEIRS"), p.ours.Named("OURS")), false);
                return;
            }
            if (p.feast >= 0)
            {
                var g = Feasts.Get(p.feast, out _);
                if (g != null && g.Live) return;
                // The feast was let go before they came (the weather, a raid): the one to be married walks in alone.
                p.feast = -1;
                p.alone = true;
            }
            if (!p.alone)
            {
                Host(h, p, map);
                if (p.feast < 0 && now > p.due + MatchMath.DayTicks) p.alone = true;
                return;
            }
            if (MatchMath.ArriveNow(now, p.due, GenLocalDate.HourOfDay(map), MapFacts.ThreatOn(map))) Arrive(h, p, map, null);
        }

        /// <summary>A feast's stage moved (Feasts.StageChanged): a wedding's guests at the gate bring the one to be married with them; a feast let go before they came leaves them to walk in alone.</summary>
        internal static void OnStage(GuestFeast g, Map map, FeastStage stage)
        {
            if (g == null || g.purpose != FeastPurpose.Wedding || !MatchMath.ParseFeastTag(g.tag, out int id)) return;
            var p = Get(id, out var h);
            if (p == null || p.stage != MatchStage.Agreed || p.lives != MatchHome.Colony) return;
            if (stage == FeastStage.Here)
            {
                var near = g.AllGuests.FirstOrDefault(x => x != null && x.Spawned && x.Map == map);
                // Not free to come with them (on the road elsewhere, held): they come by themselves once they can.
                if (!Arrive(h, p, map, near?.Position)) p.alone = true;
            }
            else if (stage == FeastStage.Done)
            {
                p.feast = -1;
                p.alone = true;
            }
        }

        /// <summary>Free to come to the colony now: here already, or out in the world (not with a caravan, not held, not carried off).</summary>
        private static bool CanCome(Pawn x, Map map) =>
            x != null && !x.Dead && !x.IsPrisoner && (x.Spawned ? x.Map == map : Find.WorldPawns.Contains(x) && x.GetCaravan() == null && !People.Kidnapped(x));

        /// <summary>
        /// The one to be married walks in (with the guests, or alone from the edge) and joins the colony there and then.
        /// False when they can't come now (they come by themselves later, or the match lapses).
        /// </summary>
        private static bool Arrive(DiplomacyHome h, MatchPlan p, Map map, IntVec3? near)
        {
            var x = p.theirs;
            if (!CanCome(x, map) || !Arrivals.Join(x, map, near)) return false;
            WorldLedger.Release(x, KeepFor.Spouse);
            // The council's own question about how to hold a wedding isn't asked of this one: it is held today.
            var comp = CouncilGameComponent.Instance;
            string key = WishWorker_ShareBed.PairKey(p.ours, x);
            if (comp?.weddingAsked != null && !comp.weddingAsked.Contains(key)) comp.weddingAsked.Add(key);
            p.stage = MatchStage.Here;
            p.arrived = Now;
            string rite = MatchWords.Rite(p.people, map);
            // apart: told as they walk in; the wedding is held on the map, by the game's own ceremony.
            Tell.Letter(NewsKind.World, Importance.Important, map, MatchWords.P("CC_Match_Arrive_Label", x.Named("THEIRS")),
                MatchWords.T(p.alone || p.feast < 0 ? "CC_Match_Arrive_Alone" : "CC_Match_Arrive", x.Named("THEIRS"), p.ours.Named("OURS"),
                    MatchWords.People(p.people).Named("PEOPLE"), rite.Named("RITE")), LetterDefOf.PositiveEvent, new LookTargets(x), p.people);
            Diplomacy.Dirty();
            return true;
        }

        /// <summary>
        /// The two at the colony, engaged: the game's own ceremony from five in the morning to four in the afternoon, when the
        /// game would hold one (no danger, both ready); the guests stand to watch (WeddingSpot). Not held within three days, the
        /// council's own wedding plan holds it on a fine evening (Social/WeddingPlans.cs); within eight more, let go.
        /// </summary>
        private static void Ceremony(DiplomacyHome h, MatchPlan p, Map map, int now)
        {
            var a = p.ours;
            var b = p.theirs;
            if (now - p.arrived > (3 + MatchMath.GiveUpDays) * MatchMath.DayTicks)
            {
                Off(h, p, MatchWords.P("CC_Match_Off_NoWedding", a.Named("OURS"), b.Named("THEIRS")), false);
                return;
            }
            if (!a.Spawned || !b.Spawned || a.Map != map || b.Map != map || Ceremonies(map, a) != null) return;
            if (now - p.arrived > 3 * MatchMath.DayTicks)
            {
                if (!WeddingPlans.Planned(a, b)) WeddingPlans.Plan(a, b, WeddingPlans.Quiet);
                return;
            }
            // With the wedding's guests here, not from the feast's hour on: the feast begins then, and either one begun would
            // keep the other from starting until it fell through (the ceremony waits for the next morning instead).
            bool guests = p.feast >= 0 && Feasts.Get(p.feast, out _) is GuestFeast g && g.OnMap;
            if (!MatchMath.CeremonyHour(GenLocalDate.HourOfDay(map), guests)) return;
            try
            {
                if (map.lordsStarter == null || !MarriageCeremonyUtility.AcceptableGameConditionsToStartCeremony(map) || !MarriageCeremonyUtility.FianceReadyToStartCeremony(a, b)
                    || !MarriageCeremonyUtility.FianceReadyToStartCeremony(b, a))
                    return;
                map.lordsStarter.TryStartMarriageCeremony(a, b);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: the ceremony", e);
            }
        }

        /// <summary>The game's own wedding ceremony this pawn is being married at, on the map, or null.</summary>
        private static Lord Ceremonies(Map map, Pawn x)
        {
            if (map?.lordManager == null || x == null) return null;
            foreach (var l in map.lordManager.lords)
                if (l?.LordJob is LordJob_Joinable_MarriageCeremony j && (j.firstPawn == x || j.secondPawn == x))
                    return l;
            return null;
        }

        /// <summary>
        /// Where a wedding's guests stand (Feasts.HereFocus): by the couple while the game's ceremony is under way, the party
        /// after the vows too (the match is a marriage by then), else at the gate. The feast waits while it lasts: the game's
        /// ceremony lets no other gathering begin, so a feast begun under it would wait and fall through.
        /// </summary>
        internal static IntVec3 WeddingSpot(GuestFeast g, Map map)
        {
            if (g == null || g.purpose != FeastPurpose.Wedding || !MatchMath.ParseFeastTag(g.tag, out int id)) return IntVec3.Invalid;
            var p = Get(id, out _);
            if (p != null)
                return p.stage == MatchStage.Here && p.ours != null && p.ours.Spawned && p.ours.Map == map && Ceremonies(map, p.ours) != null ? p.ours.Position : IntVec3.Invalid;
            // Married already: the couple's party after the vows goes on, the guests by them.
            var people = g.guests.Select(x => x?.people).FirstOrDefault(x => x != null);
            foreach (var t in With(people))
            {
                var ours = OursOf(t);
                if (ours != null && ours.Spawned && ours.Map == map && Ceremonies(map, ours) != null) return ours.Position;
            }
            return IntVec3.Invalid;
        }

        // ================= Their kin come for the colony's own =================

        /// <summary>
        /// Ours to go to them: on the day (by daylight, no danger, ours at home and free), the one they marry and one of their
        /// kin walk in to the gate (LordJob_MatchEscort). Not at home within some days of it, it is let go with nobody at fault.
        /// </summary>
        private static void Fetch(DiplomacyHome h, MatchPlan p, Map map, int now)
        {
            if (now < p.due) return;
            if (now > p.due + MatchMath.GiveUpDays * MatchMath.DayTicks)
            {
                Off(h, p, MatchWords.P("CC_Match_Off_NotHome", p.ours.Named("OURS"), MatchWords.People(p.people).Named("PEOPLE")), false);
                return;
            }
            if (!p.ours.Spawned || p.ours.Map != map || p.ours.Downed) return;
            if (!MatchMath.ArriveNow(now, p.due, GenLocalDate.HourOfDay(map), MapFacts.ThreatOn(map))) return;
            SendEscort(h, p, map);
        }

        /// <summary>The one they marry (free to travel) and one of their kin walk in from the edge to the gate.</summary>
        private static void SendEscort(DiplomacyHome h, MatchPlan p, Map map)
        {
            if (!Arrivals.TryEntryCell(map, out var entry)) return;
            var gate = PlaySpots.GateCell(map, null);
            if (!gate.IsValid) gate = DropCellFinder.TradeDropSpot(map);
            var party = new List<Pawn>();
            try
            {
                var x = p.theirs;
                if (!x.Spawned && x.Faction == p.people && !x.IsPrisoner && Arrivals.Spawn(x, map, entry)) party.Add(x);
                var kind = Arrivals.PersonKind(p.people);
                string last = x.Name is NameTriple tri ? tri.Last : null;
                if (kind != null)
                {
                    var kin = Arrivals.Make(kind, p.people, new FloatRange(30f, 60f), null, last, false, x);
                    if (kin != null)
                    {
                        GenSpawn.Spawn(kin, CellFinder.RandomClosewalkCellNear(entry, map, 4), map);
                        party.Add(kin);
                    }
                }
                foreach (var y in party) Hosting.Provision(y, 2f);
                if (party.Count == 0) return;
                LordMaker.MakeNewLord(p.people, new LordJob_MatchEscort(p.id, gate), map, party);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: their kin come", e);
                return;
            }
            p.stage = MatchStage.Here;
            p.arrived = -1;
            // apart: told as they walk in; the colony's own walks out to them at the gate.
            Tell.Message(NewsKind.World, Importance.Important, map, MatchWords.T("CC_Match_Escort_Comes", MatchWords.Name(p.theirs, p.theirsName).Named("THEIRS"),
                p.ours.Named("OURS"), MatchWords.People(p.people).Named("PEOPLE"), (p.townName ?? "?").Named("TOWN")), new LookTargets(party), MessageTypeDefOf.NeutralEvent);
            Diplomacy.Dirty();
        }

        /// <summary>Their kin at the gate (LordJob_MatchEscort): the colony's own walks out to them.</summary>
        internal static void EscortArrived(LordJob_MatchEscort j)
        {
            var p = Get(j.plan, out var h);
            if (p == null || p.stage != MatchStage.Here) return;
            p.arrived = Now;
            SendOut(p, j.lord);
        }

        /// <summary>The colony's own told to walk out to their kin at the gate (Rimfolk_MatchLeave), when free to.</summary>
        private static void SendOut(MatchPlan p, Lord l)
        {
            var c = p.ours;
            var x = l?.ownedPawns.FirstOrDefault(y => y != null && y.Spawned && !y.Downed);
            if (c == null || x == null || !c.Spawned || c.Map != x.Map || c.Downed || c.InMentalState || c.Drafted || MatchDefOf.Rimfolk_MatchLeave == null) return;
            if (c.CurJobDef == MatchDefOf.Rimfolk_MatchLeave) return;
            var job = JobMaker.MakeJob(MatchDefOf.Rimfolk_MatchLeave, x);
            c.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        /// <summary>Their kin waiting: the colony's own sent out when free; waited too long, they go home without them, and the colony's word is broken.</summary>
        private static void EscortHour(DiplomacyHome h, MatchPlan p, Map map, int now)
        {
            var l = map.lordManager?.lords.FirstOrDefault(x => x?.LordJob is LordJob_MatchEscort j && j.plan == p.id);
            if (l == null)
            {
                // Gone from the map without the colony's own (driven off, every one of them lost).
                Off(h, p, MatchWords.P("CC_Match_Off_KinGone", MatchWords.People(p.people).Named("PEOPLE"), p.ours.Named("OURS")), false);
                return;
            }
            if (p.arrived < 0) return;
            if (now - p.arrived > MatchMath.EscortWaitHours * MatchMath.HourTicks)
            {
                if (l.LordJob is LordJob_MatchEscort j) j.done = true;
                Off(h, p, MatchWords.P("CC_Match_Off_NotSent", p.ours.Named("OURS"), MatchWords.People(p.people).Named("PEOPLE")), true);
                return;
            }
            SendOut(p, l);
        }

        /// <summary>The colony's own at the gate, face to face with their kin, ready to go (this session's; a reload sends them out again).</summary>
        private static readonly Dictionary<int, Pawn> leaving = new Dictionary<int, Pawn>();

        internal static void ReadyToLeave(Pawn c, Pawn x)
        {
            if (c != null && x != null) leaving[c.thingIDNumber] = x;
        }

        /// <summary>Their kin's group, on its tick: the colony's own who is ready goes with them.</summary>
        internal static void TakeLeave(LordJob_MatchEscort j)
        {
            var p = Get(j.plan, out _);
            if (p?.ours == null || !leaving.TryGetValue(p.ours.thingIDNumber, out var x)) return;
            leaving.Remove(p.ours.thingIDNumber);
            if (x == null || !x.Spawned || x.GetLord()?.LordJob != j || !p.ours.Spawned || p.ours.Downed || !p.ours.Position.InHorDistOf(x.Position, 4f)) return;
            Departs(p.ours, x);
        }

        /// <summary>
        /// The colony's own has walked out to their kin (the end of Rimfolk_MatchLeave, face to face): a word of farewell,
        /// they become one of their people and leave with them for good; the wedding at their town comes once they are there.
        /// </summary>
        internal static void Departs(Pawn c, Pawn x)
        {
            var p = PlanOf(c, out var h);
            var l = x?.GetLord();
            if (p == null || p.stage != MatchStage.Here || !(l?.LordJob is LordJob_MatchEscort j) || j.plan != p.id) return;
            var map = c.Map;
            try
            {
                // together: said face to face at the gate, where the job brought them.
                Outsiders.Say(c, "matchfarewell", x);
                Outsiders.Say(x, "matchescort", c);
                if (c.Drafted && c.drafter != null) c.drafter.Drafted = false;
                c.GetLord()?.RemovePawn(c);
                c.SetFaction(p.people);
                l.AddPawn(c);
                j.done = true;
                p.stage = MatchStage.Away;
                p.left = Now;
                WorldLedger.Keep(c, KeepFor.Spouse);
                WorldLedger.Keep(p.theirs, KeepFor.Spouse);
                // apart: told as they leave; they walk out of the map with their kin.
                Tell.Letter(NewsKind.World, Importance.Important, map, MatchWords.P("CC_Match_Leaves_Label", c.Named("OURS")),
                    MatchWords.T("CC_Match_Leaves", c.Named("OURS"), MatchWords.Name(p.theirs, p.theirsName).Named("THEIRS"), MatchWords.People(p.people).Named("PEOPLE"),
                        (p.townName ?? "?").Named("TOWN")), LetterDefOf.NeutralEvent, new LookTargets(c), p.people);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: leaving with their kin", e);
            }
            Diplomacy.Dirty();
        }

        /// <summary>At their town: the two married there by the game's own relation (their faith's or their age's rite), told in their letter.</summary>
        private static void WedAway(DiplomacyHome h, MatchPlan p, Map map)
        {
            try
            {
                var a = p.ours;
                var b = p.theirs;
                a.relations.TryRemoveDirectRelation(PawnRelationDefOf.Fiance, b);
                b.relations.TryRemoveDirectRelation(PawnRelationDefOf.Fiance, a);
                if (!a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, b)) a.relations.AddDirectRelation(PawnRelationDefOf.Spouse, b);
                SpouseRelationUtility.ChangeNameAfterMarriage(a, b, a.relations.nextMarriageNameChange);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: the wedding at their town", e);
            }
            Wed(h, p, map, false);
        }

        /// <summary>The game's own ceremony married two (Patch_Married): a match under way between them is a marriage alliance now.</summary>
        public static void Married(Pawn a, Pawn b)
        {
            var p = PlanOf(a, out var h);
            if (p == null || !p.Has(b) || h?.Map == null) return;
            Wed(h, p, h.Map, true);
        }

        /// <summary>
        /// The wedding: the Marriage tie (from their people to the colony, about the colony's own, "spouse:" and theirs, where
        /// they live), the deed "married", the history's "dip:wedding" (a milestone), the alliance's defence pact (never the
        /// Empire's), and the letter that says what the alliance means.
        /// </summary>
        private static void Wed(DiplomacyHome h, MatchPlan p, Map map, bool atColony)
        {
            if (p == null || !p.Live) return;
            p.stage = MatchStage.Wed;
            h?.matches.Remove(p);
            var f = p.people;
            try
            {
                var oursRef = WorldLedger.PersonOf(p.ours);
                var theirsRef = WorldLedger.PersonOf(p.theirs);
                var terms = new TieTerms { about = oursRef, key = MatchMath.SpouseKey(theirsRef.ToString()), goods = MatchMath.LivesKey(p.lives), number = Day };
                var t = WorldLedger.AddTie(Tie.Kind.Marriage, Refs.Of(f), Diplomacy.Colony(map), terms, Tie.Never, MatchMath.Origin);
                string names = MatchWords.P("CC_Match_Couple", p.ours.Named("OURS"), p.theirs.Named("THEIRS"));
                FactionMemory.Note(f, "married", names);
                var place = atColony || p.town < 0 ? Diplomacy.Colony(map) : Ref.Town(p.town);
                var actors = new List<HistoryActor>
                {
                    new HistoryActor(oursRef, HistoryRole.Did), new HistoryActor(theirsRef, HistoryRole.With), new HistoryActor(Refs.Of(f), HistoryRole.With),
                    new HistoryActor(Diplomacy.Colony(map), HistoryRole.For)
                };
                string rite = MatchWords.Rite(f, map);
                Diplomacy.Write(DiplomacyMath.Wedding, actors, place, MatchWords.P(atColony ? "CC_Match_Hist_Wed" : "CC_Match_Hist_WedAway", p.ours.Named("OURS"), p.theirs.Named("THEIRS"),
                    MatchWords.People(f).Named("PEOPLE"), (p.townName ?? "?").Named("TOWN")), true);
                // A defence pact only while a pact's own rules hold (MatchMath.AlliancePact: goodwill 20, not the Empire).
                bool pact = PactHolds(f, map) && Pacts.Of(f, map, PactKind.Defence) == null && Pacts.Make(f, map, PactKind.Defence, false, null, 0) != null;
                if (!atColony) NotableBook.Notify_Met(p.ours, "spouse", null);
                var form = MatchWords.Form(map, f);
                // Their kin among the guests only when the wedding's guests are at the colony (one who walked in alone, or a
                // wedding after the guests went home, was held without them).
                bool kin = atColony && p.feast >= 0 && Feasts.Get(p.feast, out _) is GuestFeast guests && guests.OnMap;
                string text = MatchWords.T(!atColony ? "CC_Match_WedAway" : kin ? "CC_Match_Wed" : "CC_Match_WedQuiet", p.ours.Named("OURS"), p.theirs.Named("THEIRS"),
                    MatchWords.People(f).Named("PEOPLE"), (p.townName ?? "?").Named("TOWN"), rite.Named("RITE"), form.word.Named("WORD"));
                // The pact running on is said only of one that stands (none with the setting off, or none to be made).
                bool pactHad = !pact && Pacts.On && Pacts.Of(f, map, PactKind.Defence) != null;
                text += "\n\n" + MatchWords.P(Ages.IsEmpire(f) ? "CC_Match_Wed_WhatEmpire" : pact ? "CC_Match_Wed_What" : pactHad ? "CC_Match_Wed_WhatPactHad" : "CC_Match_Wed_WhatNoPact",
                    MatchWords.People(f).Named("PEOPLE"), MatchMath.RegardBonus.Named("REGARD"));
                if (t == null) text += "\n\n" + MatchWords.P("CC_Match_Wed_NoRoom", MatchWords.People(f).Named("PEOPLE"));
                // apart: the wedding told in a letter; at the colony it was held on the map by the game's own ceremony.
                Tell.Letter(NewsKind.World, Importance.Important, map, MatchWords.P("CC_Match_Wed_Label", p.ours.Named("OURS"), p.theirs.Named("THEIRS")), text,
                    LetterDefOf.PositiveEvent, atColony ? new LookTargets(p.ours) : LookTargets.Invalid, f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: the wedding", e);
            }
            Diplomacy.Dirty();
        }
    }
}
