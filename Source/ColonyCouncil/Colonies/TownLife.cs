using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A town the colony was in its own life (docs/design/stage3-many-colonies.md, section 2, "The bad side", "Taking it
    /// back" and "Its growth"; release 5, the player's decisions 11B and 15B). On the region's hour and day while a town the
    /// colony was is on record (TownsAfter.Hourly and Daily call in), under the setting "The town's life":
    /// <list type="bullet">
    /// <item><b>Raided</b>: a world event of its own (Colonies/TownRaid.cs).</item>
    /// <item><b>Hard times.</b> Long struggling by the region's rule (RiseFallMath.Struggling) with goodwill 60 or more, its
    /// head asks to come back under the colony (a letter by the town's means; their yes stands, so "Take it back" is on the
    /// letter and the town's card); a no, or the question left ten days, is a refusal. Refused twice, its people give the
    /// town up: its former colonists first, and up to three of its other people who would join the colony, come to the
    /// colony's gate asking to come home, and the town is a ruin (the region's own, RiseFall.MakeRuin). The region never
    /// gives up a people's seat; this town is given up only by its own people, and with no other town left the people is
    /// no more (its leader steps aside, it is made temporary and defeated, as when it is taken back).</item>
    /// <item><b>Turning against the colony.</b> The colony's wrongs they remember (the faction memory, the ledger's grudges,
    /// a broken pact or truce among the wrongs) and a head who thinks little of the colony lower the town's natural
    /// goodwill (Rimfolk_TownCooled), which the game's own drift follows; a new head who thinks little of it is told.
    /// Hostile, it raids at the storyteller's rate with its people's own raiders (the game's), and the raid's letter names a
    /// former colonist the game redressed into them (Patch_TownRaidFormers); the raiders' camps may pick it by their own
    /// rule.</item>
    /// <item><b>Its growth (15B)</b>: only with Faction Territories, at that mod's own pace, through the hooks on its
    /// expansion (WorldMechanics.Founded and Built call <see cref="Founded"/> and <see cref="Built"/>); Rimfolk's own new towns
    /// and ruins settled again are never this people's.</item>
    /// <item><b>11B</b>: a former colonist dies only in a fight on a map the player sees, a raid the town lost, or a plague
    /// or famine of its people or within 20 tiles of it (once, when it ends), each told by letter with their name.</item>
    /// </list>
    /// Nothing per tick and no schedule slot. With nothing handed over, nothing here runs.
    /// </summary>
    public static class TownLife
    {
        /// <summary>The town's life (Many colonies: "The town's life"), with handing over.</summary>
        public static bool On => TownsAfter.LifeOn && HandOvers.On;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("town life: " + what, e);
            }
        }

        // ================= The hour and the day =================

        /// <summary>The region's hour: its people at the colony's gate once the day comes, and the gate's question let go once they have gone.</summary>
        public static void Hourly(int now)
        {
            foreach (var rec in WorldLedger.Formers.ToList())
            {
                if (rec == null) continue;
                if (rec.gateAt >= 0 && now >= rec.gateAt && rec.end == TownEnd.None) Safe("its people at the gate", () => Gate(rec, now));
                if (rec.gateCame >= 0) Safe("the gate's question", () => GateWatch(rec, now));
            }
        }

        /// <summary>The region's day: the town's natural goodwill, a new head told, its hard times, the region's hardships.</summary>
        public static void Daily(int now)
        {
            foreach (var rec in WorldLedger.Formers.ToList())
            {
                if (rec == null || rec.people == null) continue;
                Safe("its goodwill", () => Cool(rec));
                if (!On || !rec.Standing || rec.end != TownEnd.None) continue;
                Safe("a new head", () => NewHead(rec));
                Safe("hard times", () => HardTimes(rec, now));
                Safe("the region's hardships", () => Hardships(rec, now));
            }
        }

        // ================= Words and records =================

        /// <summary>The town's means of word by its age at the hand-over: a runner, a rider, the radio, comms.</summary>
        public static string Means(LedgerFormer rec) => ("CC_TownLife_Means_" + TownLifeMath.CallMeans(rec?.age ?? WorldAge.Medieval)).Translate().Resolve();

        /// <summary>The town's fortune moved by what struck it (a raid), on its own record wherever it lies, the cause named on its card.</summary>
        public static void MoveFortune(LedgerFormer rec, float amount, string cause)
        {
            var t = rec != null ? WorldLedger.TownRecord(rec.key) : null;
            if (t == null || !t.Seeded || Mathf.Abs(amount) < 0.01f) return;
            int now = Now;
            t.prosperity = RegionMath.Clamp(t.prosperity + amount);
            t.cause = cause;
            t.causeDir = amount > 0f ? 1 : -1;
            t.causeAt = now;
            t.lowSince = RegionMath.LowSince(t.prosperity, t.lowSince, now);
            WorldLedger.TownChanged();
        }

        /// <summary>A share of the town's trade goods taken or burned: each stack less that share (its people and animals for sale stay).</summary>
        public static void LoseGoods(Settlement town, float share)
        {
            var stock = town?.trader?.GetDirectlyHeldThings();
            if (stock == null || share <= 0f) return;
            foreach (var t in stock.ToList())
            {
                if (t == null || t is Pawn || t.Destroyed) continue;
                int take = Mathf.RoundToInt(t.stackCount * Mathf.Clamp01(share));
                if (take <= 0) continue;
                if (take >= t.stackCount)
                {
                    stock.Remove(t);
                    t.Destroy();
                }
                else t.SplitOff(take).Destroy();
            }
        }

        /// <summary>One of the colony's former people died (told by the letter that named them): the history keeps it, and the town's record lets them go.</summary>
        public static void FormerDied(LedgerFormer rec, Pawn p)
        {
            if (rec == null || p == null) return;
            var actors = new List<HistoryActor> { new HistoryActor(WorldLedger.PersonOf(p), HistoryRole.About), new HistoryActor(Ref.Colony(rec.key), HistoryRole.About) };
            if (rec.people != null) actors.Add(new HistoryActor(Refs.Of(rec.people), HistoryRole.With));
            History.Add(FoundingMath.Family + ":formerDied", actors, Ref.Town(rec.key), HistoryMath.Milestone,
                "CC_TownLife_DiedHistory".Translate(p.LabelShort.Named("PAWN"), TownsAfter.TownName(rec).Named("TOWN")).Resolve());
            rec.formers.Remove(p);
            if (rec.second == p) rec.second = null;
            WorldLedger.Release(p, KeepFor.Former);
            WorldLedger.FormerChanged();
        }

        // ================= Turning against the colony =================

        /// <summary>
        /// Worked out each day: what the colony's wrongs they remember take from the town's natural goodwill (the faction
        /// memory's unsettled wrongs, a broken pact or truce among them, and the ledger's live grudges from them), and what a
        /// head who thinks little of the colony takes.
        /// </summary>
        private static void Cool(LedgerFormer rec)
        {
            int wrongs = 0, head = 0;
            if (On && rec.end == TownEnd.None && Find.FactionManager.AllFactionsListForReading.Contains(rec.people))
            {
                float memory = FactionMemory.On ? FactionMemory.Instance?.Sum(rec.people, DeedSide.Wrong, true) ?? 0f : 0f;
                int grudges = WorldLedger.LiveTies(Tie.Kind.Grudge).Count(t => t.from == Refs.Of(rec.people));
                wrongs = TownLifeMath.WrongsPart(memory, grudges);
                var n = TownLetters.HeadOf(rec);
                head = n != null ? TownLifeMath.HeadPart(NotableBook.Opinion(n)) : 0;
            }
            if (rec.cooledWrongs == wrongs && rec.cooledHead == head) return;
            rec.cooledWrongs = wrongs;
            rec.cooledHead = head;
            WorldLedger.FormerChanged();
        }

        /// <summary>A new head of the town (the second leading after the head, or one after them): told when they think little of the colony.</summary>
        private static void NewHead(LedgerFormer rec)
        {
            var leader = rec.people.leader;
            var n = TownLetters.HeadOf(rec);
            if (leader == null || leader.Dead || n == null || n.pawn != leader || rec.headSeen == leader.thingIDNumber) return;
            bool first = rec.headSeen < 0;
            rec.headSeen = leader.thingIDNumber;
            WorldLedger.FormerChanged();
            if (first || NotableBook.Opinion(n) > TownLifeMath.CoolHead) return;
            Tell.Minor(NewsKind.World, Importance.Notable, Homes.Seat, TextVariants.Vary("CC_TownLife_CoolHead").Translate(leader.LabelShort.Named("HEAD"), rec.people.LeaderTitle.Named("TITLE"),
                TownsAfter.TownName(rec).Named("TOWN"), (rec.colonyName ?? "?").Named("COLONY")).Resolve(), TownsAfter.Look(rec));
        }

        // ================= Hard times: the town asks to come back =================

        /// <summary>Struggling long by the region's own rule (RiseFallMath.Struggling), from its record.</summary>
        public static bool Struggling(LedgerFormer rec, int now)
        {
            var t = rec != null ? WorldLedger.TownRecord(rec.key) : null;
            if (t == null || !t.Seeded || rec.people == null) return false;
            return RiseFallMath.Struggling(t.prosperity, t.lowSince, RiseFall.Blows(rec.people, now), -1, now);
        }

        /// <summary>
        /// Its question open: let go after ten days as a no. Else, long struggling with goodwill 60 or more and at peace, a
        /// season since the hand-over and its last ask, its head asks to come back under the colony.
        /// </summary>
        private static void HardTimes(LedgerFormer rec, int now)
        {
            if (rec.askOpen)
            {
                if (now - rec.askedBackAt > TownLifeMath.AskLapseDays * GenDate.TicksPerDay) Refused(rec, true);
                return;
            }
            if (rec.gateAt >= 0 || !Tell.CanAsk || Homes.Seat == null) return;
            if (!TownLifeMath.AsksBack(Struggling(rec, now), rec.people.PlayerGoodwill, rec.people.HostileTo(Faction.OfPlayer), rec.agreed, rec.refusals, rec.handedAt,
                    rec.askedBackAt, now))
                return;
            AskBack(rec, now);
        }

        /// <summary>The head asks: their yes stands from now (the letter's "Take it back", the town's card), until the colony answers or lets it go.</summary>
        internal static void AskBack(LedgerFormer rec, int now)
        {
            rec.askedBackAt = now;
            rec.askOpen = true;
            rec.agreed = true;
            rec.agreedAt = now;
            rec.agreedTold = true;
            WorldLedger.FormerChanged();
            TownLifeLetters.AskBack(rec);
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>The colony said not now, or left the question: their yes is gone, the town remembers; refused twice, its people give it up.</summary>
        public static void Refused(LedgerFormer rec, bool lapsed)
        {
            if (rec == null || !rec.askOpen) return;
            int now = Now;
            rec.askOpen = false;
            rec.agreed = false;
            rec.refusals++;
            WorldLedger.FormerChanged();
            TownLifeLetters.Remove(rec, ChoiceLetter_TownLife.Back);
            string town = TownsAfter.TownName(rec);
            if (FactionMemory.On && rec.people != null) FactionMemory.Note(rec.people, "refused", town);
            if (TownLifeMath.GivesUp(rec.refusals))
            {
                rec.gateAt = now + Rand.RangeInclusive(TownLifeMath.GateMinDays, TownLifeMath.GateMaxDays) * GenDate.TicksPerDay;
                WorldLedger.FormerChanged();
                Tell.Minor(NewsKind.World, Importance.Important, Homes.Seat, "CC_TownLife_Leaving".Translate(town.Named("TOWN"), (rec.people?.Name ?? "?").Named("PEOPLE"),
                    (rec.colonyName ?? town).Named("COLONY")).Resolve(), TownsAfter.Look(rec));
            }
            else if (lapsed)
                Tell.Minor(NewsKind.World, Importance.Notable, Homes.Seat, "CC_TownLife_AskLapsed".Translate(town.Named("TOWN")).Resolve(), TownsAfter.Look(rec));
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>Taken back by their own ask: the hand-over's way back (TownTakeBack.Make, a long event), its question answered.</summary>
        public static void TakeBack(LedgerFormer rec)
        {
            if (rec == null) return;
            string why = TownTakeBack.WhyNot(rec);
            if (why != null)
            {
                // direct: the answer to the player's own click.
                Messages.Message(why, MessageTypeDefOf.RejectInput, false);
                return;
            }
            rec.askOpen = false;
            WorldLedger.FormerChanged();
            TownLifeLetters.Remove(rec, ChoiceLetter_TownLife.Back);
            TownTakeBack.Ask(rec);
        }

        // ================= Refused twice: its people at the colony's gate =================

        /// <summary>The town's people now at a home of the colony's, waiting at its gate (of its people, on their feet or not, not the colony's).</summary>
        public static List<Pawn> AtGate(LedgerFormer rec, out Map map)
        {
            map = null;
            var list = new List<Pawn>();
            if (rec?.people == null) return list;
            foreach (var home in Homes.All)
            {
                if (home?.mapPawns == null) continue;
                foreach (var p in home.mapPawns.SpawnedPawnsInFaction(rec.people))
                    if (p.RaceProps.Humanlike && !p.Dead && p.GetLord()?.LordJob is LordJob_VisitColony)
                        list.Add(p);
                if (list.Count > 0)
                {
                    map = home;
                    break;
                }
            }
            return list;
        }

        /// <summary>
        /// Refused twice, the day has come (by day, with no fight at home, its map closed): the town is given up, a ruin of its
        /// people; its former colonists first, then up to three of its other people who would join the colony, walk in from
        /// the side of the map facing the town to the gate and ask to come home. With no other town, its people is no more.
        /// </summary>
        private static void Gate(LedgerFormer rec, int now)
        {
            var home = Homes.Seat;
            var town = rec.Town;
            if (!rec.Standing || town == null)
            {
                rec.gateAt = -1;
                WorldLedger.FormerChanged();
                return;
            }
            if (home == null || town.HasMap || MapFacts.ThreatOn(home)) return;
            int hour = GenLocalDate.HourOfDay(home);
            if (hour < 7 || hour > 18) return;
            var people = rec.people;
            string townName = TownsAfter.TownName(rec);
            var formers = rec.formers.Where(p => TownLayouts.FreeAtHome(p, people) && p.RaceProps.Humanlike).ToList();
            var joiners = TownTakeBack.Joiners(rec, people, formers);
            var all = formers.Concat(joiners).ToList();
            var order = TownLifeMath.Gate(all.Select(p => formers.Contains(p)).ToList(), all.Select(p => formers.Contains(p) ? 0f : 100f).ToList());
            var come = order.Select(i => all[i]).ToList();
            var edge = TownsAfter.EdgeToward(home, town);
            bool last = TownLifeMath.LastTown(Find.WorldObjects.Settlements.Count(o => o != null && !o.Destroyed && o.Faction == people));
            var oldHead = people.leader;
            rec.gateAt = -1;
            rec.end = TownEnd.GivenUp;
            rec.endedAt = now;
            rec.agreed = false;
            rec.askOpen = false;
            WorldLedger.FormerChanged();
            // With no other town, the people is no more: its leader steps aside first, so the game makes no stranger to lead it.
            if (last) people.leader = null;
            var ledgerTown = WorldLedger.TownRecord(rec.key);
            var ruin = RiseFall.MakeRuin(town, ledgerTown, false);
            NotableBook.Instance?.TownGoneOver(rec.key);
            // Made temporary only once its ruin (in its colour) stands, so the game lets the people go when the ruin and its
            // people are gone, never before.
            if (last)
            {
                people.defeated = true;
                people.temporary = true;
            }
            // They come to the gate.
            var spawned = new List<Pawn>();
            foreach (var p in come)
                if (Arrivals.Spawn(p, home, edge))
                    spawned.Add(p);
            if (spawned.Count > 0)
            {
                var gate = PlaySpots.GateCell(home, spawned[0]);
                if (!gate.IsValid && !RCellFinder.TryFindRandomSpotJustOutsideColony(spawned[0], out gate)) gate = spawned[0].Position;
                LordMaker.MakeNewLord(people, new LordJob_VisitOnErrand(people, gate, 2 * GenDate.TicksPerDay), home, spawned);
                rec.gateCame = now;
                WorldLedger.FormerChanged();
            }
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(people), HistoryRole.Did), new HistoryActor(Ref.Colony(rec.key), HistoryRole.About) };
            History.Add(FoundingMath.Family + ":townGivenUp", actors, Ref.Town(rec.key), HistoryMath.Milestone,
                "CC_TownLife_GivenUpHistory".Translate(townName.Named("TOWN"), people.Name.Named("PEOPLE")).Resolve());
            TownLifeLetters.AskGate(rec, home, spawned, formers, last, oldHead, ruin);
            LensModel.Dirty();
            Cards.Dirty();
        }

        /// <summary>The gate's question: let go once they have gone (unanswered, they went on their way), its letter closed.</summary>
        private static void GateWatch(LedgerFormer rec, int now)
        {
            if (AtGate(rec, out _).Count > 0) return;
            rec.gateCame = -1;
            WorldLedger.FormerChanged();
            TownLifeLetters.Remove(rec, ChoiceLetter_TownLife.Gate);
        }

        /// <summary>Taken in: those at the gate (all of them, or only the colony's own) join the colony where they stand; the rest go on.</summary>
        public static void TakeIn(LedgerFormer rec, bool formersOnly)
        {
            var here = AtGate(rec, out var map);
            if (here.Count == 0 || map == null) return;
            string town = TownsAfter.TownName(rec);
            var joined = new List<Pawn>();
            var rest = new List<Pawn>();
            foreach (var p in here)
            {
                if (formersOnly && !rec.formers.Contains(p))
                {
                    rest.Add(p);
                    continue;
                }
                bool former = rec.formers.Contains(p);
                if (!Arrivals.Join(p, map)) continue;
                joined.Add(p);
                if (!former) continue;
                rec.formers.Remove(p);
                WorldLedger.Release(p, KeepFor.Former);
                var actors = new List<HistoryActor> { new HistoryActor(WorldLedger.PersonOf(p), HistoryRole.Did), new HistoryActor(Ref.Colony(Homes.KeyOf(map)), HistoryRole.For) };
                History.Add(FoundingMath.KindHomecoming, actors, Ref.Town(rec.key), HistoryMath.Milestone,
                    "CC_TownAfter_HomeHistory".Translate(p.LabelShort.Named("PAWN"), town.Named("TOWN"), Homes.Name(map).Named("HOME")).Resolve());
            }
            if (rest.Count > 0) Arrivals.SendAway(rest);
            rec.gateCame = -1;
            WorldLedger.FormerChanged();
            if (joined.Count > 0)
                Tell.Message(NewsKind.Colony, Importance.Important, map, "CC_TownLife_GateIn".Translate(Foundings.Names(joined).Named("NAMES"), town.Named("TOWN")).Resolve(),
                    new LookTargets(joined), MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>Sent on: those at the gate go on their way.</summary>
        public static void SendOn(LedgerFormer rec)
        {
            var here = AtGate(rec, out var map);
            if (here.Count > 0) Arrivals.SendAway(here);
            rec.gateCame = -1;
            WorldLedger.FormerChanged();
            if (map != null && here.Count > 0)
                Tell.Message(NewsKind.Colony, Importance.Notable, map, "CC_TownLife_GateOn".Translate(TownsAfter.TownName(rec).Named("TOWN")).Resolve(), new LookTargets(here),
                    MessageTypeDefOf.NeutralEvent);
        }

        // ================= The region's hardships (decision 11B) =================

        /// <summary>
        /// A plague or a famine of its people's, or within 20 tiles of the town, ended since the last look: each of its former
        /// colonists free at home (not its head) takes their chance, the eldest first, one death at most; told by letter.
        /// </summary>
        private static void Hardships(LedgerFormer rec, int now)
        {
            var ws = WorldSituations.Instance;
            var town = rec.Town;
            if (ws == null || town == null) return;
            int since = Math.Max(rec.hardshipSeen, rec.handedAt), latest = since;
            foreach (var r in ws.history.ToList())
            {
                if (r == null || r.endTick <= since) continue;
                latest = Math.Max(latest, r.endTick);
                if (r.ending == WorldSituations.Quietly) continue;
                var d = DefDatabase<WorldSituationDef>.GetNamedSilentFail(r.defName ?? "");
                int hardship = TownLifeMath.HardshipKind(d?.kind);
                if (hardship == TownLifeMath.NoHardship || d?.bad != true) continue;
                var at = RiseFall.TileOf(r.place);
                float tiles = at.Valid && at.Layer == town.Tile.Layer ? Find.WorldGrid.ApproxDistanceInTiles(at, town.Tile) : -1f;
                if (!TownLifeMath.HardshipReaches(r.faction == rec.people, tiles)) continue;
                Reckon(rec, r, hardship, now);
            }
            // Each world event reckoned once: the last ending looked at.
            if (latest > rec.hardshipSeen)
            {
                rec.hardshipSeen = latest;
                WorldLedger.FormerChanged();
            }
        }

        private static void Reckon(LedgerFormer rec, SituationRecord r, int hardship, int now)
        {
            bool struggling = Struggling(rec, now);
            var who = rec.formers.Where(p => TownLayouts.FreeAtHome(p, rec.people) && p != rec.people.leader && WorldSituationWorker.FreeInWorld(p))
                .OrderByDescending(p => p.ageTracker?.AgeBiologicalYears ?? 0).ToList();
            if (who.Count == 0) return;
            var chances = who.Select(p => TownLifeMath.HardshipDeathChance(hardship, struggling, p.ageTracker?.AgeBiologicalYears ?? 30, r.helped)).ToList();
            var rolls = who.Select(_ => Rand.Value).ToList();
            int i = TownLifeMath.WhoDies(chances, rolls);
            if (i < 0) return;
            var p = who[i];
            string town = TownsAfter.TownName(rec);
            string what = (hardship == TownLifeMath.Plague ? "CC_TownLife_Of_Plague" : "CC_TownLife_Of_Famine").Translate((r.label ?? "?").Named("WHAT")).Resolve();
            if (!WorldSituationWorker.DieOffscreen(p)) return;
            string text = TextVariants.Vary("CC_TownLife_HardshipText").Translate(p.LabelShort.Named("PAWN"), town.Named("TOWN"), what.Named("WHAT"), (rec.colonyName ?? town).Named("COLONY"),
                Means(rec).Named("MEANS")).Resolve();
            if (r.helped) text += "\n\n" + "CC_TownLife_HardshipHelped".Translate(town.Named("TOWN")).Resolve();
            Tell.Letter(NewsKind.World, Importance.Important, Homes.Seat, "CC_TownLife_HardshipLabel".Translate(p.LabelShort.Named("PAWN"), town.Named("TOWN")).Resolve(), text,
                LetterDefOf.Death, TownsAfter.Look(rec));
            FormerDied(rec, p);
        }

        // ================= Its growth (15B) =================

        /// <summary>Faction Territories' builders began a town of a people the colony's town became: a line in the news, wherever it lies.</summary>
        public static void Founded(Faction people, PlanetTile tile)
        {
            if (!On || people == null || !HandOverPeoples.IsOurs(people)) return;
            var rec = WorldLedger.FormerOf(people);
            if (rec == null || rec.end != TownEnd.None) return;
            Tell.Minor(NewsKind.World, Importance.Notable, Homes.Seat, "CC_TownLife_Growing".Translate(people.Name.Named("PEOPLE"), TownsAfter.TownName(rec).Named("TOWN"),
                (rec.colonyName ?? "?").Named("COLONY")).Resolve(), tile.Valid ? new LookTargets(new GlobalTargetInfo(tile)) : LookTargets.Invalid);
        }

        /// <summary>The town finished: a town of its own, told, in the history under the colony's old town.</summary>
        public static void Built(Faction people, Settlement town)
        {
            if (!On || people == null || town == null || !HandOverPeoples.IsOurs(people)) return;
            var rec = WorldLedger.FormerOf(people);
            if (rec == null || rec.end != TownEnd.None) return;
            string mother = TownsAfter.TownName(rec);
            var actors = new List<HistoryActor> { new HistoryActor(Refs.Of(people), HistoryRole.Did), new HistoryActor(Ref.Colony(rec.key), HistoryRole.About) };
            History.Add(FoundingMath.Family + ":townGrew", actors, Refs.Of(town), HistoryMath.Everyday,
                "CC_TownLife_GrewHistory".Translate(people.Name.Named("PEOPLE"), (town.HasName ? town.Name : "?").Named("NEWTOWN"), mother.Named("TOWN")).Resolve());
            Tell.Minor(NewsKind.World, Importance.Notable, Homes.Seat, "CC_TownLife_Grew".Translate(people.Name.Named("PEOPLE"), (town.HasName ? town.Name : "?").Named("NEWTOWN"),
                mother.Named("TOWN")).Resolve(), new LookTargets(town));
        }
    }

    /// <summary>
    /// The colony's wrongs and a cool head in a town the colony was (release 5; Colonies/TownLife.cs): its natural goodwill
    /// lowered by what the colony did to it (the faction memory's wrongs, the ledger's grudges) and by a head who thinks
    /// little of the colony, as the game shows among the people's goodwill situations. The game's own drift does the rest.
    /// </summary>
    public class GoodwillSituationWorker_TownCooled : GoodwillSituationWorker
    {
        public override string GetPostProcessedLabel(Faction other)
        {
            var r = WorldLedger.FormerOf(other);
            if (r == null) return base.GetPostProcessedLabel(other);
            return (r.cooledHead > r.cooledWrongs ? "CC_TownLife_CooledHead" : "CC_TownLife_CooledWrongs").Translate((r.colonyName ?? "?").Named("COLONY")).Resolve();
        }

        public override int GetNaturalGoodwillOffset(Faction other)
        {
            if (other == null || !HandOverPeoples.IsOurs(other) || !TownLife.On) return 0;
            var r = WorldLedger.FormerOf(other);
            return r == null || r.end != TownEnd.None ? 0 : -(r.cooledWrongs + r.cooledHead);
        }
    }

    [DefOf]
    public static class TownLifeDefOf
    {
        public static GoodwillSituationDef Rimfolk_TownCooled;
        public static LetterDef Rimfolk_TownLifeLetter;

        static TownLifeDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(TownLifeDefOf));
        }
    }
}
