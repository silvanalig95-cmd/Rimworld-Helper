using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A traveller's stay at a sick town, and the sickness they carry home from it (Harm/SickHome.cs). Saved in HarmBook.</summary>
    public class SickStay : IExposable
    {
        public Pawn pawn;
        /// <summary>The town (Settlement.ID) and its name, for the words.</summary>
        public int town = -1;
        public string townName;
        public SickTown kind;
        /// <summary>Hours the caravan was found stopped at the town.</summary>
        public float hours;
        /// <summary>The caravan carried medicine while it stayed.</summary>
        public bool medicine;
        /// <summary>The last hour it was found there.</summary>
        public int lastAt;
        /// <summary>The caravan it was with (one journey, one roll).</summary>
        public int caravan = -1;
        /// <summary>The homecoming's roll came up for them: the tick it shows, and what; -1 while not yet home or rolled.</summary>
        public int due = -1;
        public string hediff;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_Values.Look(ref townName, "townName");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref hours, "hours");
            Scribe_Values.Look(ref medicine, "medicine");
            Scribe_Values.Look(ref lastAt, "lastAt");
            Scribe_Values.Look(ref caravan, "caravan", -1);
            Scribe_Values.Look(ref due, "due", -1);
            Scribe_Values.Look(ref hediff, "hediff");
        }
    }

    /// <summary>
    /// Sickness brought home (the harm review's 3.1.1). A caravan of the colony's found stopped at another people's town
    /// that lies under a plague (its people's own, or one within twenty tiles: the region's world events, as the town's own
    /// hardships read them) or a famine counts its hours there, each traveller's. Two hours or more is lodging there (a
    /// quick trade at the gate isn't). When they come home, the journey has one roll: one in six for a night's stay (eight
    /// hours), less for a shorter one; half with medicine packed, half with industrial or spacer medicine, half again
    /// under the quarantine decree, a famine half a plague; times the harm scale. If it comes up, one of them (the cap
    /// letting them be hurt) falls ill two to four days later: a plague's sickness is the game's flu, or its plague one
    /// time in four; a famine's the everyday stomach bug, with the everyday ailments on. Told in a letter naming the town,
    /// with a line over their head. An onset the cap holds back waits up to three days, then passes.
    /// </summary>
    public static class SickHome
    {
        public static bool On => Harm.On;

        private static int Now => Find.TickManager.TicksGame;

        private static readonly Color SayColor = new Color(0.85f, 0.95f, 0.75f);

        /// <summary>The town's sickness now: a plague over a famine, from the region's running world events.</summary>
        public static SickTown TownSickness(Settlement s)
        {
            var ws = WorldSituations.Instance;
            if (s?.Faction == null || ws == null) return SickTown.None;
            var kind = SickTown.None;
            foreach (var w in ws.Running)
            {
                if (w?.def == null || !w.def.bad) continue;
                var k = HarmTwoMath.KindOf(w.def.kind, w.def.defName == "Council_WS_Murrain");
                if (k == SickTown.None || (int)k <= (int)kind) continue;
                if (!HarmTwoMath.Reaches(w.ctx?.faction == s.Faction, TilesTo(w, s))) continue;
                kind = k;
            }
            return kind;
        }

        /// <summary>How far the world event lies from the town, in tiles (-1 when it has no place on this layer).</summary>
        private static float TilesTo(WorldSituation w, Settlement s)
        {
            try
            {
                var at = PlanetTile.Invalid;
                var town = Refs.Town(w.place);
                if (town != null) at = town.Tile;
                else at = RiseFall.TileOf(w.place);
                if (!at.Valid || at.Layer != s.Tile.Layer) return -1f;
                return Find.WorldGrid.ApproxDistanceInTiles(at, s.Tile);
            }
            catch
            {
                return -1f;
            }
        }

        /// <summary>The hour's look (HarmBook): caravans stopped at sick towns, homecomings, and sicknesses that show.</summary>
        public static void Hourly(HarmBook book, int now)
        {
            if (!On) return;
            try
            {
                Stays(book, now);
                Homecomings(book, now);
                Onsets(book, now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: sickness brought home", e);
            }
        }

        private static void Stays(HarmBook book, int now)
        {
            foreach (var c in Find.WorldObjects.Caravans)
            {
                if (c == null || !c.IsPlayerControlled || c.pather == null || c.pather.Moving) continue;
                var s = Find.WorldObjects.SettlementAt(c.Tile);
                if (s?.Faction == null || s.Faction.IsPlayer) continue;
                var kind = TownSickness(s);
                if (kind == SickTown.None) continue;
                bool medicine = CaravanInventoryUtility.AllInventoryItems(c).Any(t => t?.def != null && t.def.IsMedicine);
                foreach (var p in c.PawnsListForReading)
                {
                    if (p == null || p.Dead || p.RaceProps == null || !p.RaceProps.Humanlike || !(p.IsColonist || p.IsSlaveOfColony)) continue;
                    var stay = book.sick.FirstOrDefault(x => x.pawn == p && x.town == s.ID && x.due < 0);
                    if (stay == null) book.sick.Add(stay = new SickStay { pawn = p, town = s.ID, townName = s.Name });
                    stay.hours += 1f;
                    stay.lastAt = now;
                    stay.caravan = c.ID;
                    stay.kind = HarmTwoMath.Worse(stay.kind, kind);
                    stay.medicine |= medicine;
                }
            }
        }

        private static bool Home(Pawn p) => p != null && p.Spawned && !p.Dead && p.Map != null && p.Map.IsPlayerHome;

        /// <summary>A journey's travellers home: one roll for the journey (its stays with the same caravan and town).</summary>
        private static void Homecomings(HarmBook book, int now)
        {
            var waiting = book.sick.Where(x => x.due < 0 && Home(x.pawn)).ToList();
            foreach (var journey in waiting.GroupBy(x => (x.caravan, x.town)).ToList())
            {
                var stays = book.sick.Where(x => x.due < 0 && x.caravan == journey.Key.caravan && x.town == journey.Key.town).ToList();
                var home = stays.Where(x => Home(x.pawn)).ToList();
                foreach (var x in stays) book.sick.Remove(x);
                if (home.Count == 0) continue;
                var kind = home.Max(x => x.kind);
                float hours = home.Max(x => x.hours);
                bool medicine = home.Any(x => x.medicine);
                bool modern = Ages.Ours >= WorldAge.Industrial;
                var quarantine = DefDatabase<DecreeDef>.GetNamedSilentFail("Council_Decree_Quarantine");
                bool kept = quarantine != null && home.Any(x => DecreeTracker.InForceFor(x.pawn, quarantine));
                if (!Harm.Roll(HarmTwoMath.SickHome(kind, hours, medicine, modern, kept))) continue;
                var who = home.Where(x => Harm.May(x.pawn, HurtClass.Injury)).Select(x => x).RandomElementWithFallback();
                var def = SicknessFor(kind, who?.pawn);
                if (who == null || def == null) continue;
                who.due = now + HarmTwoMath.OnsetTicks(Rand.Value);
                who.hediff = def.defName;
                book.sick.Add(who);
            }
        }

        /// <summary>A plague's: the game's flu, or its plague one time in four. A famine's: the everyday stomach bug.</summary>
        private static HediffDef SicknessFor(SickTown kind, Pawn p)
        {
            if (kind == SickTown.Plague)
            {
                var flu = DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
                var plague = HediffDefOf.Plague;
                return Rand.Chance(HarmTwoMath.PlagueShare) && plague != null ? plague : flu ?? plague;
            }
            if (kind == SickTown.Famine && AilmentTracker.On) return DefDatabase<HediffDef>.GetNamedSilentFail("Council_Ail_StomachBug");
            return null;
        }

        private static void Onsets(HarmBook book, int now)
        {
            foreach (var x in book.sick.Where(x => x.due >= 0 && now >= x.due).ToList())
            {
                var p = x.pawn;
                var def = x.hediff == null ? null : DefDatabase<HediffDef>.GetNamedSilentFail(x.hediff);
                if (p == null || p.Dead || def == null || HarmTwoMath.OnsetLapsed(x.due, now))
                {
                    book.sick.Remove(x);
                    continue;
                }
                // At home and the cap letting them be hurt: it shows. Otherwise it waits (a sickbed, a hurt lately, still away).
                if (!Home(p) || !Harm.May(p, HurtClass.Injury)) continue;
                book.sick.Remove(x);
                var h = Harm.Ail(p, def, Math.Max(0.05f, def.initialSeverity), "sickness brought home from " + x.townName, HurtClass.Injury);
                if (h == null) continue;
                Tell(p, def, x);
            }
        }

        private static void Tell(Pawn p, HediffDef def, SickStay x)
        {
            string ill = AilmentTracker.NameOf(def) ?? def.label;
            string tag = x.kind == SickTown.Plague ? "plague" : "famine";
            string line = Flavor.FromPool("Rimfolk_Pool_SickHome", p, null, null, tag);
            if (!line.NullOrEmpty()) Speech.Say(p, line.Formatted(p.Named("PAWN")).Resolve(), null, SayColor);
            string why = (x.kind == SickTown.Plague ? "CC_HarmTwo_SickHome_Plague" : "CC_HarmTwo_SickHome_Famine").Translate((x.townName ?? "?").Named("TOWN")).Resolve();
            string text = TextVariants.Vary("CC_HarmTwo_SickHome").Translate(p.Named("PAWN"), ill.Named("ILLNESS"), (x.townName ?? "?").Named("TOWN"), why.Named("WHY")).Resolve();
            string note = AilmentTracker.DoctorsNote(def);
            if (!note.NullOrEmpty()) text += " " + note;
            ColonyCouncil.Tell.Letter(NewsKind.Health, Importance.Important, p.Map, ill.CapitalizeFirst() + ": " + p.LabelShortCap, text, LetterDefOf.NegativeEvent, new LookTargets(p));
        }

        /// <summary>The day's pruning: stays long past (the road home taken without them coming home), the dead.</summary>
        public static void Prune(HarmBook book, int now)
        {
            book.sick.RemoveAll(x => x == null || x.pawn == null || x.pawn.Dead || x.due < 0 && !HarmTwoMath.StayKept(x.lastAt, now));
        }
    }
}
