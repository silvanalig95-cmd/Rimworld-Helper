using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Colonists sometimes ask, rarely, for better clothes ("Colonists ask for better clothes"; the player's words, 9 November
    /// 2026: "colonists sometimes ask, rarely"): when the colony's choice gives one a piece lying in storage (or in their pack,
    /// or a piece their outfit doesn't allow) that fills a gap of theirs (warmth they lack for the cold ahead, armour or a
    /// helmet when raids come often, a worn-out piece) or is clearly better (twice the gear tab's gain), they now and then say
    /// so in one sentence in their own voice (Rimfolk_Pool_ApparelAsk: the piece by name, where it lies, and why, by the
    /// line's tag; every age, every tone): "It's freezing; that parka in the stockpile would do me good." In their free time at
    /// home, never in danger, drafted, downed or not themselves; at most once in six days each and once a day in the colony,
    /// the same piece not again for fifteen days, never while their suggestions are set aside; warmth twice as likely while
    /// it is colder outside than they can bear. Never one that would take off their own lucky piece or heirloom. The piece is
    /// checked to lie where the line says at that moment. A quiet row stays in the inbox (How people are) while it stands.
    /// </summary>
    public static class ApparelAsks
    {
        public const string Pool = "Rimfolk_Pool_ApparelAsk";

        public static bool On => ApparelLive.On && (ColonyCouncilMod.Settings?.apparelAsks ?? true);

        /// <summary>The hourly look for one colonist (the pass's slice): a piece worth asking for, the waits over, the chance; then their line.</summary>
        public static void Look(Pawn p, ApparelPick.Picks picks, int now)
        {
            if (!On || picks == null || !WeaponAsks.FreeAndSafe(p)) return;
            var book = ApparelPickBook.Instance;
            if (book == null) return;
            string me = ApparelPick.Key(p);
            var s = picks.Of(p).Where(x => ApparelAskMath.Askable(x) && ApparelPick.Stands(picks, x) && !ApparelPickMath.GivesUpKeepsake(x, me)
                                           && (x.Kind == WearKind.Wear || x.Kind == WearKind.Pack || x.Kind == WearKind.Outfit))
                .OrderBy(x => x.Gap.HasValue ? 0 : 1).ThenByDescending(x => x.Gain).FirstOrDefault();
            if (s == null) return;
            var t = picks.Thing(s.Piece);
            var c = picks.input.Colonist(me);
            var last = book.AskOf(p);
            if (!ApparelAskMath.MayAsk(now, last?.tick ?? -1, book.lastAsk, last?.piece ?? -1, t.thingIDNumber, book.NotNowUntil(p))) return;
            if (!Rand.Chance(ApparelAskMath.ChanceFor(s, c?.ColdNow ?? 0f))) return;
            string place = s.Kind == WearKind.Pack ? "CC_ApparelAsk_InPack".Translate().Resolve() : PlaceNow(picks, t);
            string line = Line(p, s, t, place);
            if (line.NullOrEmpty()) return;
            Speech.Say(p, line);
            book.Asked(p, t, place, s.Gain, s.Gap == WearGap.Warmth);
        }

        /// <summary>Where it lies now, as the line says it: "in the stockpile", "on the shelf", "on the outfit stand".</summary>
        private static string PlaceNow(ApparelPick.Picks picks, Thing t) =>
            t.ParentHolder is Building_OutfitStand stand ? "CC_WeaponPick_On".Translate(stand.def.label.Named("LABEL")).Resolve() : WeaponPick.PlaceOf(t);

        /// <summary>
        /// Their line: tagged for why (warmth, cooling, armour, helmet, worn, dead, faith, title, role, work, mechanoids, crowds,
        /// better); {APPAREL} its label, {PLACE} where it lies. Only a line for this reason (its words must be true); else one
        /// that only says it is better.
        /// </summary>
        public static string Line(Pawn p, WearSuggestion s, Thing t, string place)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(Pool);
            if (pool?.lines == null) return null;
            var tags = Flavor.TagsFor(p);
            string why = ApparelPickMath.AskTag(s);
            tags.Add(why);
            if (!Flavor.AnyFits(pool.lines.AllLines(), tags))
            {
                tags.Remove(why);
                tags.Add("better");
                if (!Flavor.AnyFits(pool.lines.AllLines(), tags)) return null;
            }
            string raw = pool.lines.Pick(ToneUtility.ToneFor(p), tags, p);
            if (raw.NullOrEmpty()) return null;
            return raw.Formatted(p.Named("PAWN"), t.LabelNoCount.Named("APPAREL"), place.Named("PLACE")).Resolve().CapitalizeFirst();
        }

        // ---------------- The inbox ----------------

        /// <summary>
        /// How people are: a quiet row for each colonist who asked lately (three days) while their suggestion still stands and
        /// isn't set aside: "Bautista asked for the parka in the stockpile, for the cold", its reasons under it, a click opening
        /// their gear tab, its act the order itself ("Wear"; for a piece their outfit doesn't allow, "Allow"). Counted
        /// nowhere: it waits on no one.
        /// </summary>
        public static void InboxRows(List<WeekRow> rows)
        {
            var book = ApparelPickBook.Instance;
            if (book == null || !On || book.asks.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            foreach (var a in book.asks)
            {
                if (!ApparelAskMath.Shows(now, a.tick, 0)) continue;
                var p = ApparelPick.Colonists(Find.CurrentMap).FirstOrDefault(x => x.thingIDNumber == a.pawn)
                        ?? Homes.All.SelectMany(ApparelPick.Colonists).FirstOrDefault(x => x.thingIDNumber == a.pawn);
                if (p == null || ApparelPickBook.Instance.IsNotNow(p)) continue;
                var picks = ApparelPick.For(p.Map);
                var s = picks?.Of(p).FirstOrDefault(x => picks.Thing(x.Piece)?.thingIDNumber == a.piece && x.Kind != WearKind.Trade);
                if (s == null || !ApparelPick.Stands(picks, s)) continue;
                var picksNow = picks;
                var asked = s;
                rows.Add(new WeekRow
                {
                    pawn = p,
                    text = (a.warmth ? "CC_ApparelAsk_InboxWarmth" : "CC_ApparelAsk_Inbox").Translate(WeekModel.Name(p).Named("PAWN"), ApparelPick.Label(picks, s.Piece).Named("APPAREL"),
                        (a.place ?? "").Named("PLACE"), ApparelPickMath.GainText(s.Gain).Named("GAIN")).Resolve(),
                    sub = ApparelPick.Why(picks, s).CapitalizeFirst(),
                    tip = WeekModel.Tip(ApparelPick.Full(picks, s), "CC_WeaponAsk_InboxClick".Translate(p.Named("PAWN")).Resolve()),
                    click = WeekModel.Inspect(p, typeof(ITab_Pawn_Gear)),
                    waits = false,
                    order = -a.tick,
                    act = ApparelPickGearTab.ActLabel(picks, s),
                    actTip = ApparelPickGearTab.ActTip(picks, s),
                    actRun = () =>
                    {
                        ApparelPick.Do(picksNow, asked);
                        ApparelPickGearTab.Refresh();
                    }
                });
            }
        }
    }
}
