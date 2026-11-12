using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // ===================== 20. Books kept from the fire (the learned society) =====================

    /// <summary>
    /// Books kept from the fire (item 20), round 1's goods held in trust (PeopleTrust, TrustRecord) carrying real books: when a
    /// society town burns or falls (a ruin in the ledger, a great fire near one of its towns), a copyist brings a cart of the
    /// archive and asks the colony to keep it for a season. The books are the game's own (and Vanilla Books Expanded's
    /// papers), set down by the colony's stores, where its readers may read them by the game's own reading (or kept locked
    /// away, forbidden); Anomaly's tome comes in a sealed crate and stays sealed. A cart fetches them later: every book there
    /// is thanks and one book left behind; one lost or sold (or the tome's seal broken) is a lasting wrong.
    /// </summary>
    public static class ArchiveTrust
    {
        public const string Kind = "pev:archive";
        public const string Means = "cart";
        public const string Left = "left";

        private static int Now => Find.TickManager.TicksGame;

        private static IEnumerable<TrustRecord> Records => PeopleEventsBook.Instance?.trusts?.Where(r => r != null && r.means == Means) ?? Enumerable.Empty<TrustRecord>();

        /// <summary>An archive held in trust now (one at a time).</summary>
        public static TrustRecord Open() => Records.FirstOrDefault(r => !r.done);

        /// <summary>
        /// The archive's books (and the tome's crate) on this map, still theirs: what the fetch carries out. The fetch closes
        /// the record as their cart comes, and the books are carried out after, so a fetched record's books count until they
        /// are handed over; the one left with the colony, those sold and a lapsed archive's are the colony's.
        /// </summary>
        public static IEnumerable<Thing> Held(Map map)
        {
            if (map == null) return Enumerable.Empty<Thing>();
            return Records.Where(r => r.map == map && r.ink != "lapsed")
                .SelectMany(r => r.crates.Where(t => t != null && !t.Destroyed && t.Spawned && t.Map == map && r.FateOf(t.thingIDNumber) == null)).Distinct().ToList();
        }

        /// <summary>One of the archive's things (a book or the tome's crate) still held in trust.</summary>
        public static bool IsHeld(Thing t)
        {
            if (t == null) return false;
            foreach (var r in Records)
                if (!r.done && r.ids.Contains(t.thingIDNumber) && r.FateOf(t.thingIDNumber) == null)
                    return true;
            return false;
        }

        /// <summary>
        /// The society's trouble now: a town of theirs fallen within the last month (a ruin in the ledger, not the colony's
        /// doing), or a great fire raging near one of their towns. Its town's name, or null.
        /// </summary>
        public static string Trouble(Faction society, Map map, out bool fire)
        {
            fire = false;
            if (society == null) return null;
            try
            {
                var ruins = WorldLedger.Instance?.Ruins;
                if (ruins != null)
                    foreach (var r in ruins)
                        if (r?.people != null && Peoples.CallingOf(r.people) == PeoplesMath.Scholars && !r.byColony && r.since >= 0 && Now - r.since <= 30 * GenDate.TicksPerDay)
                            return r.name;
                var all = WorldSituations.Instance?.Running;
                if (all != null)
                    foreach (var s in all)
                    {
                        if (s?.def?.defName != "Council_WS_GreatFire") continue;
                        var at = ThingRefs.TileOf(ThingRefs.Of(s.place));
                        var town = Find.WorldObjects.Settlements.Where(t => t.Faction == society && at.Valid && t.Tile.Layer == at.Layer)
                            .OrderBy(t => Find.WorldGrid.ApproxDistanceInTiles(t.Tile, at)).FirstOrDefault();
                        if (s.ctx?.faction == society || town != null && Find.WorldGrid.ApproxDistanceInTiles(town.Tile, at) <= 8f)
                        {
                            fire = true;
                            return town?.Name ?? s.ctx?.placeName ?? society.Name;
                        }
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the society's trouble", e);
            }
            return null;
        }

        /// <summary>The archive's books: the game's own, by the society's age (with Vanilla Books Expanded its papers), each of decent make.</summary>
        public static List<Thing> Books(int age, int n)
        {
            var list = new List<Thing>();
            bool papers = DefDatabase<ThingDef>.GetNamedSilentFail("VBE_Newspaper") != null;
            int start = Rand.Range(0, 4);
            for (int i = 0; i < n; i++)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(PeopleEvents2Math.ArchiveBook(age, start + i, papers)) ?? DefDatabase<ThingDef>.GetNamedSilentFail("TextBook");
                if (def == null) continue;
                try
                {
                    var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                    t.TryGetComp<CompQuality>()?.SetQuality(new[] { QualityCategory.Normal, QualityCategory.Good, QualityCategory.Excellent }.RandomElement(), ArtGenerationContext.Outsider);
                    list.Add(t);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 2: an archive's book", e);
                }
            }
            return list;
        }

        /// <summary>
        /// The archive left: its books (and with Anomaly the tome in its sealed crate) carried in by the copyist's people and
        /// set down by the colony's stores; the record kept; the fetch one of the events' visits. <paramref name="locked"/>:
        /// kept locked away (forbidden), nobody reads them.
        /// </summary>
        public static TrustRecord Take(HappeningContext ctx, bool locked)
        {
            var map = ctx?.map;
            var book = PeopleEventsBook.Instance;
            var f = ctx?.faction;
            if (map == null || book == null || f == null || Open() != null) return null;
            var things = Books(PeopleEvents.AgeOf(f), Mathf.Max(PeopleEvents2Math.ArchiveMin, ctx.count));
            if (ModsConfig.AnomalyActive && DefDatabase<ThingDef>.GetNamedSilentFail(PeopleEvents2Math.Tome) != null && PeopleTrust.MakeCrate(PeopleEvents2Math.Tome + ":1") is TrustCrate tome)
                things.Add(tome);
            if (things.Count == 0) return null;
            var rec = new TrustRecord
            {
                id = book.nextTrust++, faction = f, map = map, left = Now,
                due = Now + Rand.RangeInclusive(PeopleEvents2Math.ArchiveMinDays, PeopleEvents2Math.ArchiveMaxDays) * GenDate.TicksPerDay, fee = 0, master = ctx.stranger?.LabelShort, means = Means
            };
            foreach (var t in things)
            {
                if (t is TrustCrate c) c.trust = rec.id;
                rec.crates.Add(t);
                rec.ids.Add(t.thingIDNumber);
            }
            book.trusts.Add(rec);
            while (book.trusts.Count > PeopleEventsMath.MaxTrusts && book.trusts.Any(r => r.done)) book.trusts.Remove(book.trusts.First(r => r.done));
            var at = PeopleTrust.Stores(map);
            try
            {
                Couriers.Loot(things, map, ctx.stranger != null && ctx.stranger.Spawned ? ctx.stranger.Position : at, f, PeoplesG.Up(ctx.stranger, map) ? ctx.stranger : null, at);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the archive carried in", e);
                foreach (var t in things.Where(t => !t.Spawned && !t.Destroyed)) GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
            }
            if (locked)
                foreach (var t in things.Where(t => !(t is TrustCrate)))
                    t.SetForbidden(true, false);
            var v = CouncilGameComponent.Instance?.happenings?.AddVisit(Kind, ctx.incident, map, null, null, null, rec.due + 3 * GenDate.TicksPerDay);
            if (v != null)
            {
                v.stake = rec.id;
                // Kept locked away: the books stay forbidden while they wait (the porters set them down unforbidden).
                if (locked) v.text = "locked";
            }
            FactionMemory.Note(f, "sheltered", "CC_PEv2_ArchiveName".Translate().Resolve());
            return rec;
        }

        /// <summary>The fetch's hour: waiting until it's due, then their cart; the trust lapses when their people can no longer come.</summary>
        public static bool Hour(Visit v, int now)
        {
            var rec = PeopleEventsBook.Instance?.trusts?.FirstOrDefault(r => r != null && r.id == v.stake);
            if (rec == null || rec.done) return false;
            if (now < rec.due)
            {
                if (v.text == "locked" && rec.map != null)
                    foreach (var t in rec.crates.Where(t => t != null && t.Spawned && t.Map == rec.map && !(t is TrustCrate) && rec.FateOf(t.thingIDNumber) == null && !t.IsForbidden(Faction.OfPlayer)))
                        t.SetForbidden(true, false);
                return true;
            }
            var map = rec.map ?? v.map;
            if (map == null || rec.faction == null || rec.faction.defeated || rec.faction.HostileTo(Faction.OfPlayer))
            {
                rec.done = true;
                rec.ink = "lapsed";
                if (map != null)
                    Tell.Message(NewsKind.World, Importance.Notable, map, "CC_PEv2_ArchiveLapsed".Translate((rec.faction?.Name ?? "?").Named("FACTION")).Resolve(), LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
                return false;
            }
            if (map.dangerWatcher.DangerRating != StoryDanger.None && now < rec.due + 2 * GenDate.TicksPerDay) return true;
            Fetch(rec, map, v.incident);
            return false;
        }

        /// <summary>The archive's things off the shelves (a bookcase holds its books inside it): set down beside it for the cart's people.</summary>
        private static void Unshelve(TrustRecord rec, Map map)
        {
            foreach (var t in rec.crates.Where(t => t != null && !t.Destroyed && !t.Spawned && t.MapHeld == map).ToList())
            {
                try
                {
                    if (t.ParentHolder is Building b && b.Spawned && t.holdingOwner != null) t.holdingOwner.TryDrop(t, b.Position, map, ThingPlaceMode.Near, out _);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 2: a book off the shelf", e);
                }
            }
        }

        /// <summary>
        /// Their cart at the gate: every book counted (on this map, sold, the tome's seal broken, lost), the verdict, one book
        /// left with the colony and the rest carried out to them, the thanks or the lasting wrong.
        /// </summary>
        public static void Fetch(TrustRecord rec, Map map, IncidentDef incident)
        {
            var f = rec.faction;
            Unshelve(rec, map);
            int total = rec.ids.Count, sold = 0, opened = 0, present = 0;
            var here = rec.crates.Where(c => c != null && !c.Destroyed && c.MapHeld == map).ToList();
            foreach (int id in rec.ids)
            {
                string fate = rec.FateOf(id);
                if (fate == PeopleEventsMath.Sold) sold++;
                else if (fate == PeopleEventsMath.Opened) opened++;
                else if (here.Any(c => c.thingIDNumber == id)) present++;
            }
            bool kept = PeopleEvents2Math.Kept(total, present, sold, opened);
            rec.done = true;
            rec.ink = kept ? "good" : "red";
            // Their copyist and a porter or two with the cart.
            var porters = new List<Pawn>();
            try
            {
                for (int i = 0; i < Mathf.Clamp(Mathf.CeilToInt(present / 4f), 1, 3); i++)
                {
                    var p = Arrivals.Make(PeoplesD.KindNamed(f, i == 0 ? "Rimfolk_Scholars_Fellow" : "Rimfolk_Scholars_Proctor"), f, new FloatRange(22f, 60f));
                    if (p != null) porters.Add(p);
                }
                if (porters.Count == 0 || Arrivals.Visit(porters, map, f, PeoplesD.GateSpot(map), 10f) == null) porters.Clear();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: the archive's cart", e);
                porters.Clear();
            }
            var x = porters.FirstOrDefault();
            // One book left with the colony for its keeping, the best of those there, when the archive came home whole.
            Thing gift = null;
            if (kept)
            {
                gift = here.Where(t => !(t is TrustCrate)).OrderByDescending(t => t.MarketValue).FirstOrDefault();
                if (gift != null)
                {
                    rec.Note(gift.thingIDNumber, Left);
                    if (gift.Spawned) gift.SetForbidden(false, false);
                }
            }
            int going = here.Count(t => t != gift);
            foreach (var t in here.Where(t => t.Spawned)) t.SetForbidden(false, false);
            if (going > 0)
            {
                var ctx = new HappeningContext { map = map, faction = f, stranger = x, incident = incident, count = going };
                // apart: a colonist carries the archive's books out to their copyist at the gate, who takes them.
                new DilemmaEffect_CarryOut { what = PlayCarry.Archive, count = going, steward = false, who = PlayWho.None, label = "CC_PEv2_ArchiveLabel".Translate().Resolve() }.Apply(ctx);
            }
            string text;
            if (kept)
            {
                if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents2Math.ArchiveGoodwill);
                FactionMemory.Note(f, "keptWord", "CC_PEv2_ArchiveName".Translate().Resolve());
                text = "CC_PEv2_ArchiveKept".Translate(f.Name.Named("FACTION"), total.Named("TOTAL"), (gift?.LabelNoCount ?? "?").Named("BOOK"), (x?.LabelShort ?? rec.master ?? "?").Named("X")).Resolve();
            }
            else
            {
                if (f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, -PeopleEvents2Math.ArchiveGoodwill);
                FactionMemory.Note(f, "robbed", "CC_PEv2_ArchiveName".Translate().Resolve(), PeopleEvents2Math.LostWeight);
                var parts = new List<string>();
                if (sold > 0) parts.Add("CC_PEv2_ArchiveSold".Translate(sold).Resolve());
                if (opened > 0) parts.Add("CC_PEv2_ArchiveOpened".Translate().Resolve());
                int lost = total - present - sold - opened;
                if (lost > 0) parts.Add("CC_PEv2_ArchiveLost".Translate(lost).Resolve());
                text = "CC_PEv2_ArchiveWronged".Translate(f.Name.Named("FACTION"), total.Named("TOTAL"), TextUtil.JoinAnd(parts).Named("WHAT"), (x?.LabelShort ?? rec.master ?? "?").Named("X")).Resolve();
            }
            if (x != null) PeoplesG.SayLine(x, incident, kept ? "kept" : "wronged");
            Tell.Letter(NewsKind.World, Importance.Notable, map, (kept ? "CC_PEv2_ArchiveKeptTitle" : "CC_PEv2_ArchiveWrongedTitle").Translate().Resolve(), text,
                kept ? LetterDefOf.PositiveEvent : LetterDefOf.NegativeEvent, x != null && x.Spawned ? new LookTargets(x) : LookTargets.Invalid);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.World, text, null));
        }

        /// <summary>Dev mode: the archive's cart comes now.</summary>
        public static bool FetchNow()
        {
            var r = Open();
            if (r == null) return false;
            r.due = Now;
            return true;
        }
    }

    /// <summary>
    /// A copyist of {FACTION} at the gate with a cart of the archive saved from {PLACE} ({COUNT} books), asking the colony
    /// to keep it for a season.
    /// </summary>
    public class HappeningWorker_ArchiveTrust : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            var f = PeoplesD.Friendly(PeoplesMath.Scholars);
            return f != null && ArchiveTrust.Open() == null && ArchiveTrust.Trouble(f, map, out _) != null && map.zoneManager.AllZones.OfType<Zone_Stockpile>().Any() && Colonists(map).Count > 0;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Scholars);
            if (ctx.faction == null) return false;
            ctx.placeName = ArchiveTrust.Trouble(ctx.faction, ctx.map, out bool fire);
            if (ctx.placeName == null) return false;
            ctx.note = fire ? "fire" : "fell";
            ctx.count = PeopleEvents2Math.ArchiveBooks(Rand.Value);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Scholars_Fellow"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            ctx.group.Add(Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Scholars_Proctor"), ctx.faction, ext.ages));
            ctx.group.RemoveAll(p => p == null);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Intellectual) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(ctx.note ?? "fell");
            if (ModsConfig.AnomalyActive) tags.Add("tome");
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "keep" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The archive taken in trust (ArchiveTrust.Take): carried in and set down by the stores; <see cref="locked"/>: kept locked away, unread.</summary>
    public class DilemmaEffect_ArchiveKeep : DilemmaEffect
    {
        public bool locked;

        public override bool ChanceEach => false;

        public override string Unavailable(DilemmaContext ctx) => ArchiveTrust.Open() != null ? "CC_PEv2_ArchiveOpen".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            sheet.Play("CC_PEv2_Impact_Archive".Translate(hc.count, hc.stranger?.LabelShort ?? "?").Resolve(), "CC_PEv2_Impact_ArchiveBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Other, locked ? 0 : 1, (locked ? "CC_PEv2_Impact_ArchiveLocked" : "CC_PEv2_Impact_ArchiveRead").Translate().Resolve(), "CC_PEv2_Impact_ArchiveReadBrief".Translate().Resolve(), "pev2archiveread");
            if (ModsConfig.AnomalyActive) sheet.Add(ImpactKind.Other, 0, "CC_PEv2_Impact_ArchiveTome".Translate().Resolve(), "CC_PEv2_Impact_ArchiveTomeBrief".Translate().Resolve(), "pev2tome");
            sheet.Add(ImpactKind.Goodwill, 1, "CC_PEv2_Impact_ArchiveThanks".Translate(hc.faction?.Name ?? "?", PeopleEvents2Math.ArchiveGoodwill).Resolve(), "CC_PEv2_Impact_ArchiveThanksBrief".Translate().Resolve(), "pev2archivethanks");
            sheet.Add(ImpactKind.Goodwill, -1, "CC_PEv2_Impact_ArchiveRisk".Translate(hc.faction?.Name ?? "?").Resolve(), "CC_PEv2_Impact_ArchiveRiskBrief".Translate().Resolve(), "pev2archiverisk");
        }

        public override void Apply(DilemmaContext ctx) => ArchiveTrust.Take(ctx as HappeningContext, locked);
    }

    /// <summary>A book of the archive sold to a trader: the record says so as it happens (the fetch tells it).</summary>
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.PreTraded))]
    public static class Patch_ArchiveSold
    {
        public static void Postfix(ThingWithComps __instance, TradeAction action)
        {
            if (!RimfolkPatches.On || action != TradeAction.PlayerSells || !(__instance is Book)) return;
            try
            {
                if (!ArchiveTrust.IsHeld(__instance)) return;
                ArchiveTrust.Open()?.Note(__instance.thingIDNumber, PeopleEventsMath.Sold);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 2: a book of the archive sold", e);
            }
        }
    }
}
