using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>A beast nemesis that fled the colony's land hurt: where from, when, how it stands (fleeing, away, back), when it comes back, how often it has.</summary>
    public class BeastFlight : IExposable
    {
        public Pawn beast;
        public int home = HomesMath.None;
        public int fledTick = -1, returnAt = -1;
        /// <summary>fleeing (running for the edge), away (in the wild, to come back), back (on the land again, or lying where it fell).</summary>
        public string state = "fleeing";
        public int flights, returns;

        public void ExposeData()
        {
            Scribe_References.Look(ref beast, "beast");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref fledTick, "fledTick", -1);
            Scribe_Values.Look(ref returnAt, "returnAt", -1);
            Scribe_Values.Look(ref state, "state", "fleeing");
            Scribe_Values.Look(ref flights, "flights");
            Scribe_Values.Look(ref returns, "returns");
        }
    }

    /// <summary>Someone close to one a beast killed or maimed has sworn to bring it down: who, against which beast, for whom, and how it ended.</summary>
    public class BeastOath : IExposable
    {
        public Pawn sworn;
        public Pawn beast;
        public string beastName, victimName;
        public int tick = -1, endTick = -1, furyAt = -1;
        public bool ended;
        /// <summary>kept (they brought it down), others (someone else did), tamed, vanished, lapsed.</summary>
        public string how;

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving && beast != null && beast.Discarded) beast = null;
            Scribe_References.Look(ref sworn, "sworn");
            Scribe_References.Look(ref beast, "beast");
            Scribe_Values.Look(ref beastName, "beastName");
            Scribe_Values.Look(ref victimName, "victimName");
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref endTick, "endTick", -1);
            Scribe_Values.Look(ref furyAt, "furyAt", -1);
            Scribe_Values.Look(ref ended, "ended");
            Scribe_Values.Look(ref how, "how");
        }
    }

    /// <summary>
    /// Animal nemeses (the player's pick "alphas and bosses", 7 November 2026), on top of the nemeses Rimfolk already keeps
    /// (Strangers/Nemeses.cs: a wild animal that downs, maims or kills a colonist and gets away gets a name and is
    /// remembered). Here: one that killed or maimed carries the scars of it (a visible hediff, nothing more); badly hurt
    /// and still on its feet, a beast nemesis turns and runs for the edge (any mental state ends), and once off the map it
    /// is kept in the world and comes back another day (6 to 16 days at the setting's 100%, twice at most), at the map's
    /// edge, prowling, or one time in three for blood (the game's own manhunting, with a letter); someone close to the one
    /// it killed or maimed (a partner or kin, a bond of 30, an opinion of 60) may swear to bring it down (three times in
    /// five), a vendetta against the one beast: their journal and a word said aloud, the life goal "settle the score"
    /// within their reach, a sworn one's fury while it is on their map, and how the oath ended (kept, someone else's
    /// hand, tamed, gone). Its end is the nemesis's own (its letter, the chronicle, the calendar). A downed or dying beast
    /// never flees, comes back or is sworn against: only once it is up again.
    /// </summary>
    public static class BeastNemeses
    {
        public const string OathPool = "Rimfolk_Pool_BeastOath";

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && S.nemeses && BeastsMath.Rate(S.beastNemeses) > 0f && Current.Game != null;

        private const int Day = BeastsMath.DayTicks;

        /// <summary>
        /// This beast ran off the colony's land hurt and is out in the wild, or still running for the edge: the nemeses
        /// tell it as got away even before it was named (Strangers/Nemeses.cs).
        /// </summary>
        public static bool Fled(Pawn x)
        {
            var book = BeastBook.Instance;
            return x != null && book != null && book.flights.Any(f => f.beast == x && (f.state == "fleeing" || f.state == "away"));
        }

        /// <summary>An open oath of theirs against a beast (the life goal "settle the score" counts it).</summary>
        public static bool Sworn(Pawn p)
        {
            var book = BeastBook.Instance;
            return p != null && book != null && book.oaths.Any(o => !o.ended && o.sworn == p);
        }

        // ---------------- The short pass ----------------

        /// <summary>Every 250 ticks: a beast nemesis badly hurt turns and runs; one that ran is off the map; a sworn one meets it.</summary>
        public static void Rare(BeastBook book, int now)
        {
            var comp = CouncilGameComponent.Instance;
            var nem = comp?.nemeses;
            if (nem == null) return;
            if (On)
                foreach (var n in nem.all)
                {
                    if (n == null || n.ended || !n.IsBeast) continue;
                    var x = n.pawn;
                    if (!BeastBook.Up(x) || x.Faction != null || !Homes.IsHome(x.Map)) continue;
                    var f = book.flights.FirstOrDefault(fl => fl.beast == x);
                    if (f != null && f.state == "fleeing") continue;
                    if (!BeastsMath.Flees(x.health.summaryHealth.SummaryHealthPercent, x.Downed, x.Dead, x.Faction != null)) continue;
                    Flee(book, x, f, now);
                }
            foreach (var f in book.flights.Where(fl => fl.state == "fleeing").ToList())
            {
                var x = f.beast;
                if (x == null || x.Dead || x.Discarded)
                {
                    book.flights.Remove(f);
                    continue;
                }
                if (x.Spawned)
                {
                    // Brought down before it reached the edge: it didn't get away.
                    if (x.Downed) f.state = "back";
                    continue;
                }
                f.state = "away";
                f.returnAt = now + Mathf.RoundToInt(BeastsMath.ReturnDays(S.beastNemeses, Rand.Value) * Day);
                Keep(x);
                // The nemeses tell a first flight as got away; one already known that runs again is told here.
                var n = nem.For(x);
                if (n != null && n.announced && f.flights > 1)
                    News.Post("CC_BeastNem_FledAgain".Translate(x.Named("X")).Resolve(), null, NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
            Fury(book, now);
        }

        /// <summary>It turns and runs for the nearest edge, out of any fury it was in.</summary>
        private static void Flee(BeastBook book, Pawn x, BeastFlight f, int now)
        {
            try
            {
                if (x.InMentalState) x.MentalState?.RecoverFromState();
                x.GetLord()?.RemovePawn(x);
                LordMaker.MakeNewLord(null, new LordJob_ExitMapNear(x.Position, LocomotionUrgency.Jog, 12f, false, false), x.Map, new List<Pawn> { x });
                if (f == null)
                {
                    f = new BeastFlight { beast = x, home = Homes.KeyOf(x.Map) };
                    book.flights.Add(f);
                }
                f.state = "fleeing";
                f.fledTick = now;
                f.flights++;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beast nemesis flees", e);
            }
        }

        private static void Keep(Pawn x)
        {
            if (x != null && Find.WorldPawns.Contains(x)) Find.WorldPawns.ForcefullyKeptPawns.Add(x);
        }

        private static void LetGo(Pawn x)
        {
            if (x != null && !x.Dead && !WorldLedger.Held(x, false)) Find.WorldPawns?.ForcefullyKeptPawns.Remove(x);
        }

        /// <summary>A sworn one on the same map as the beast they swore against (and it up): their fury, once a visit.</summary>
        private static void Fury(BeastBook book, int now)
        {
            if (BeastsDefOf.Rimfolk_BeastOathFury == null) return;
            foreach (var o in book.oaths)
            {
                if (o.ended || !BeastBook.Up(o.beast) || o.sworn == null || !o.sworn.Spawned || o.sworn.Map != o.beast.Map || o.sworn.Dead) continue;
                if (o.furyAt >= 0 && now - o.furyAt < Day) continue;
                if (o.sworn.health.hediffSet.GetFirstHediffOfDef(BeastsDefOf.Rimfolk_BeastOathFury) != null) continue;
                o.sworn.health.AddHediff(BeastsDefOf.Rimfolk_BeastOathFury);
                o.furyAt = now;
                Notify.Message("CC_BeastNem_Fury".Translate(o.sworn.Named("PAWN"), o.beast.Named("X")).Resolve(), new LookTargets(o.sworn), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
        }

        // ---------------- The hour ----------------

        /// <summary>The hour: a beast away long enough comes back; a newly named one is scarred and sworn against; oaths end.</summary>
        public static void Hourly(BeastBook book, int now)
        {
            var comp = CouncilGameComponent.Instance;
            var nem = comp?.nemeses;
            if (nem == null) return;
            Returns(book, nem, now);
            if (On) Named(book, comp, now);
            Oaths(book, comp, now);
        }

        private static void Returns(BeastBook book, NemesisTracker nem, int now)
        {
            foreach (var f in book.flights.Where(fl => fl.state == "away").ToList())
            {
                var x = f.beast;
                var n = x != null ? nem.For(x) : null;
                if (x == null || x.Dead || x.Discarded || n == null || n.ended)
                {
                    LetGo(x);
                    book.flights.Remove(f);
                    continue;
                }
                if (x.Spawned)
                {
                    f.state = "back";
                    continue;
                }
                if (now < f.returnAt) continue;
                if (!On || !BeastsMath.MayReturn(S.beastNemeses, f.returns))
                {
                    // It stays out in the wild: the world may forget it, and the nemesis ends when it does.
                    LetGo(x);
                    book.flights.Remove(f);
                    continue;
                }
                var map = Homes.MapOf(f.home) ?? Homes.Of(n.victim) ?? Homes.Seat;
                if (map == null || MapFacts.InVacuum(map)) continue;
                // Healed enough not to turn and run again the moment it is back, and the land quiet.
                if (x.Downed || x.health.summaryHealth.SummaryHealthPercent < BeastsMath.FleeBelow + 0.15f || MapFacts.ThreatOn(map) || now - map.storyState.LastThreatBigTick < Day / 2 || !map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(x.def))
                {
                    f.returnAt = now + Day / 2;
                    continue;
                }
                Return(f, n, x, map);
            }
        }

        /// <summary>It comes back at the map's edge: prowling, or one time in three for blood (a letter says so; the nemeses tell the rest).</summary>
        private static void Return(BeastFlight f, Nemesis n, Pawn x, Map map)
        {
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Animal)) return;
            try
            {
                if (Find.WorldPawns.Contains(x)) Find.WorldPawns.RemovePawn(x);
                GenSpawn.Spawn(x, cell, map);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beast nemesis comes back", e);
                return;
            }
            f.returns++;
            f.state = "back";
            if (!BeastsMath.ForBlood(Rand.Value)) return;
            bool mad = x.mindState?.mentalStateHandler != null && x.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter, null, true, false, false, null, false, false, false);
            if (!mad) return;
            string text = "CC_BeastNem_Blood".Translate(x.Named("X"), (n.victimName ?? "?").Named("VICTIM"), n.DeedText.Named("DEED")).Resolve();
            Tell.Letter(NewsKind.World, Importance.Important, map, "CC_BeastNem_BloodTitle".Translate(x.Named("X")).Resolve(), text, LetterDefOf.ThreatSmall, new LookTargets(x));
        }

        /// <summary>A beast nemesis newly named (and up, or away): its scars, and someone close to the one it killed or maimed may swear against it.</summary>
        private static void Named(BeastBook book, CouncilGameComponent comp, int now)
        {
            foreach (var n in comp.nemeses.Open.Where(n => n.IsBeast).ToList())
            {
                var x = n.pawn;
                if (x == null || x.Dead || book.noted.Contains(x.thingIDNumber)) continue;
                // Lying downed: not yet, only once it is up again or away.
                if (x.Spawned && x.Downed) continue;
                book.noted.Add(x.thingIDNumber);
                if (!BeastsMath.Grave(n.deed)) continue;
                if (BeastsDefOf.Rimfolk_BeastScars != null && x.health.hediffSet.GetFirstHediffOfDef(BeastsDefOf.Rimfolk_BeastScars) == null)
                    x.health.AddHediff(BeastsDefOf.Rimfolk_BeastScars);
                if (Rand.Chance(BeastsMath.Chance(S.beastNemeses, BeastsMath.OathChance))) Swear(book, comp, n, now);
            }
        }

        /// <summary>The closest of those close to the one it hurt (grown, able to fight, not sworn already) swears to bring it down.</summary>
        public static BeastOath Swear(BeastBook book, CouncilGameComponent comp, Nemesis n, int now)
        {
            var x = n.pawn;
            var victim = n.victim;
            if (x == null || victim == null || comp == null) return null;
            Pawn best = null;
            float bestScore = float.MinValue;
            string tie = "friend";
            foreach (var p in PawnsFinder.AllMaps_FreeColonists)
            {
                if (p == victim || !People.HasInnerLife(p) || p.DevelopmentalStage.Juvenile() || p.IsQuestLodger() || p.relations == null) continue;
                if (p.WorkTagIsDisabled(WorkTags.Violent) || book.oaths.Any(o => !o.ended && o.sworn == p)) continue;
                var rel = p.GetRelations(victim).ToList();
                bool kin = rel.Any(r => r.familyByBloodRelation);
                bool partner = rel.Any(r => r == PawnRelationDefOf.Spouse || r == PawnRelationDefOf.Lover || r == PawnRelationDefOf.Fiance);
                float strength = comp.social.ties.Where(t => t.announced && t.Between(p, victim) && t.kind != TieKind.Rivals).Select(t => t.strength).DefaultIfEmpty(0f).Max();
                int opinion = p.relations.OpinionOf(victim);
                if (!BeastsMath.Close(kin || partner, strength, opinion)) continue;
                float score = BeastsMath.Closeness(kin || partner, strength, opinion);
                if (score <= bestScore) continue;
                best = p;
                bestScore = score;
                tie = partner ? "partner" : kin ? "kin" : "friend";
            }
            if (best == null) return null;
            var o = new BeastOath { sworn = best, beast = x, beastName = x.LabelShort, victimName = n.victimName ?? victim.LabelShort, tick = now };
            book.oaths.Add(o);
            // apart: the oath is the sworn one's own; the one it hurt is named, not met (dead, or wherever they are).
            using var _cause = Causes.Push("CC_Cause_Nemesis".Translate(x.LabelShort, n.DeedText).Resolve());
            var args = new[] { best.Named("PAWN"), x.Named("X"), o.victimName.Named("VICTIM"), n.DeedText.Named("DEED") };
            string line = Flavor.FromPool(OathPool, best, null, null, n.deed ?? "maimed", tie);
            if (!line.NullOrEmpty() && best.Spawned) Speech.Say(best, line.Formatted(args).Resolve());
            string text = "CC_BeastNem_Sworn".Translate(args).Resolve();
            Notify.Message(text, new LookTargets(best), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, best));
            comp.journals.WriteTagged(best, "Rimfolk_Journal_BeastOath", new[] { tie, n.deed ?? "maimed" }, null, x.Named("X"), o.victimName.Named("VICTIM"));
            comp.aspirations.Notify_Event(best, "nemesis");
            return o;
        }

        /// <summary>How each open oath stands: kept, someone else's hand, tamed, gone, or the sworn one no longer here to keep it.</summary>
        private static void Oaths(BeastBook book, CouncilGameComponent comp, int now)
        {
            foreach (var o in book.oaths.Where(o => !o.ended).ToList())
            {
                var p = o.sworn;
                var x = o.beast;
                if (p == null || p.Dead || !p.IsColonist)
                {
                    End(o, "lapsed", now);
                    continue;
                }
                if (x == null || x.Discarded || (x.Destroyed && !x.Dead))
                {
                    End(o, "vanished", now);
                    comp.journals.WriteTagged(p, "Rimfolk_Journal_BeastOathEnd", new[] { "vanished" }, null, o.beastName.Named("NAME"), o.victimName.Named("VICTIM"));
                    continue;
                }
                // apart: how the oath ended, in the sworn one's own heart and journal; the beast is named, not met.
                if (x.Dead)
                {
                    var slayer = BeastBook.Slayer(x);
                    bool kept = slayer == p;
                    End(o, kept ? "kept" : "others", now);
                    CouncilGameComponent.GiveThought(p, kept ? BeastsDefOf.Rimfolk_Beast_OathKept : BeastsDefOf.Rimfolk_Beast_OathOthers);
                    comp.journals.WriteTagged(p, "Rimfolk_Journal_BeastOathEnd", new[] { kept ? "kept" : "others" }, null, o.beastName.Named("NAME"), o.victimName.Named("VICTIM"));
                    comp.aspirations.Notify(p, "nemesis");
                    string text = (kept ? "CC_BeastNem_OathKept" : "CC_BeastNem_OathOthers").Translate(p.Named("PAWN"), o.beastName.Named("X"), o.victimName.Named("VICTIM")).Resolve();
                    Notify.Message(text, new LookTargets(p), MessageTypeDefOf.PositiveEvent, Importance.Notable);
                    comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, p));
                    continue;
                }
                if (x.Faction == Faction.OfPlayer)
                {
                    End(o, "tamed", now);
                    comp.journals.WriteTagged(p, "Rimfolk_Journal_BeastOathEnd", new[] { "tamed" }, null, o.beastName.Named("NAME"), o.victimName.Named("VICTIM"));
                    continue;
                }
                if (comp.nemeses.For(x) == null) End(o, "lapsed", now);
            }
        }

        private static void End(BeastOath o, string how, int now)
        {
            o.ended = true;
            o.how = how;
            o.endTick = now;
        }

        /// <summary>The day: flights whose nemesis has ended let go.</summary>
        public static void Daily(BeastBook book, int now)
        {
            var nem = CouncilGameComponent.Instance?.nemeses;
            if (nem == null) return;
            foreach (var f in book.flights.ToList())
            {
                var x = f.beast;
                if (x != null && !x.Dead && !x.Discarded && nem.For(x) != null) continue;
                if (f.state == "away") LetGo(x);
                book.flights.Remove(f);
            }
        }
    }
}
