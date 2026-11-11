using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Faith in the heart (docs/design/gods.md, "Worship": the heart; brainstorm 20). A prayer comforts (Prayers.Said). One that
    /// goes unanswered (the sick died, the rain never came, someone close fell in the raid) is a crisis of faith about a third
    /// of the time (WorshipMath.CrisisChance): a hard few days, and with Ideology their certainty shaken. Two crises in a
    /// month and they may turn to another god, a rival of the one that failed them where there is one: they pray to that one
    /// now, and with Ideology lean toward the faith that holds it. Scoffers mock what the believers saw (an offering gone,
    /// an answer, a sign), face to face and only when the two are together, and the believer is hurt by it and the god
    /// offended; once in a few days at a home. A couple who pray to different gods bicker over whose god sent the rain when it
    /// comes after a dry spell. Each is a small thing: a mood or an opinion, a line over their heads, the news.
    /// </summary>
    public static class FaithHeart
    {
        // ================= An unanswered prayer =================

        public static void Unanswered(WorshipBook b, PrayerRecord r, Pawn p, God g, Map map)
        {
            if (p == null || p.Dead || g == null || r.answered) return;
            // The colony's book: it didn't come, as the one who prayed tells it (a few days apart a person).
            string words = ("CC_Wsh_Book_Unanswered_" + r.kind).Translate(Worship.Called(g, p).Named("GOD"), (r.aboutName ?? "").Named("ABOUT")).Resolve();
            Worship.Witness(g, map, GodActKind.Favour, words, p.PositionHeld);
            if (Interplay.Ready("worship-book|" + p.thingIDNumber, WorshipMath.BookGapDays)) GodLore.Prayed(p, g, words);
            if (!Rand.Chance(WorshipMath.CrisisChance)) return;
            int now = Worship.Now;
            b.AddMark(p, "crisis", g.id);
            using (Causes.Push("CC_Wsh_Cause_Unanswered".Translate(Worship.Called(g).Named("GOD"), (r.aboutName ?? "").Named("ABOUT")).Resolve()))
                CouncilGameComponent.GiveThought(p, WorshipDefOf.Rimfolk_Worship_Unanswered);
            if (Faiths.On) Faiths.Shake(p, WorshipMath.CrisisShake);
            if (p.Spawned) Worship.Say(p, "Rimfolk_Pool_Worship_Crisis", g, null, "pray:" + r.kind.ToString().ToLowerInvariant());
            string line = ("CC_Wsh_Crisis_" + r.kind).Translate(p.LabelShort.Named("PAWN"), Worship.Called(g).Named("GOD"), (r.aboutName ?? "").Named("ABOUT")).Resolve();
            CouncilGameComponent.Instance?.journals.WriteText(p, Worship.Line("Rimfolk_Pool_Worship_CrisisJournal", p, g) ?? line);
            Tell.Minor(NewsKind.People, Importance.Notable, map, line, p);
            int crises = b.Count(p, "crisis", now - WorshipMath.CrisisWindowDays * GenDate.TicksPerDay);
            if (WorshipMath.Turns(crises, Rand.Value)) TurnAway(b, p, g, map);
        }

        /// <summary>
        /// They turn from the god that failed them to another: one the stories set against it where they know one, else another
        /// the colony's people pray to, else another god they have heard of. Told, in their journal, the chronicle and the colony's book.
        /// </summary>
        public static God TurnAway(WorshipBook b, Pawn p, God from, Map map)
        {
            var pool = new List<God>();
            pool.AddRange(Gods.Rivals(from).Where(x => x.known && x.kind != GodKind.Dead));
            if (pool.Count == 0) pool.AddRange(Worship.PrayedTo(map).Where(x => x != from && x.kind != GodKind.Dead));
            if (pool.Count == 0) pool.AddRange(Gods.All.Where(x => x != from && x.known && x.kind != GodKind.Dead));
            var to = pool.Count == 0 ? null : pool.RandomElement();
            if (to == null) return null;
            b.AddMark(p, "turned", to.id);
            string text = "CC_Wsh_Turned".Translate(p.LabelShort.Named("PAWN"), Worship.Called(from).Named("FROM"), Worship.Called(to).Named("GOD")).Resolve();
            Tell.Message(NewsKind.People, Importance.Notable, map, text, p, MessageTypeDefOf.NeutralEvent);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, text, p));
            Worship.Witness(to, map, GodActKind.Favour, text, p.PositionHeld);
            // The god turned from takes it as such; the one turned to is glad of it.
            Worship.Favour(from, map, -3f, "CC_Wsh_Why_TurnedFrom".Translate(p.LabelShort.Named("PAWN")).Resolve(), FavourSource.Neglect, GodOffence.Rivals);
            Worship.Favour(to, map, 3f, "CC_Wsh_Why_TurnedTo".Translate(p.LabelShort.Named("PAWN")).Resolve(), FavourSource.Rite);
            return to;
        }

        // ================= Scoffers =================

        /// <summary>
        /// Something believers saw a god do near this place (an offering taken, a prayer answered): a scoffer who is with a
        /// believer of that god says what they think of it, to their face; the believer is hurt, and the god offended.
        /// </summary>
        public static void Witnessed(Map map, God g, IntVec3 cell)
        {
            if (map == null || g == null || !cell.IsValid) return;
            try
            {
                var near = map.mapPawns.FreeColonistsSpawned.Where(p => Worship.Ours(p) && !p.DevelopmentalStage.Baby() && p.Position.InHorDistOf(cell, 25f)).ToList();
                var scoffer = near.Where(Worship.Scoffer).InRandomOrder().FirstOrDefault();
                if (scoffer == null) return;
                var believer = near.Where(p => p != scoffer && !Worship.Scoffer(p) && Worship.GodOf(p) == g && Together.Can(scoffer, p)).InRandomOrder().FirstOrDefault();
                if (believer == null || !Interplay.Ready("worship-mock|" + Gods.HomeKey(map), WorshipMath.MockGapDays)) return;
                Mock(scoffer, believer, g, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a scoffer", e);
            }
        }

        private static void Mock(Pawn scoffer, Pawn believer, God g, Map map)
        {
            // together: Witnessed found them side by side (Together.Can), and Speech.Say checks it again.
            if (!Worship.Say(scoffer, "Rimfolk_Pool_Worship_Scoff", g, believer)) return;
            Worship.Say(believer, "Rimfolk_Pool_Worship_ScoffReply", g, scoffer);
            CouncilGameComponent.GiveThought(believer, WorshipDefOf.Rimfolk_Worship_Mocked, scoffer);
            Worship.Favour(g, map, WorshipMath.MockFavour, "CC_Wsh_Why_Mocked".Translate(scoffer.LabelShort.Named("PAWN")).Resolve(), FavourSource.Neglect, GodOffence.Scoffing);
        }

        // ================= Couples and the rain =================

        /// <summary>The region's hour: rain falling after a dry spell (or an answered prayer's rain) sets couples of two gods arguing over whose it was.</summary>
        public static void Hourly(WorshipBook b, Map map, int now)
        {
            var w = map.weatherManager?.curWeather;
            if (w == null || w.rainRate <= 0.1f) return;
            var book = WeatherBook.Of(map);
            if (book?.DaysBefore == null || book.DaysBefore.Count == 0) return;
            // The dry days before today: rain today after three or more.
            int dryBefore = 0;
            for (int i = book.DaysBefore.Count - 1; i >= 0 && book.DaysBefore[i].Dry; i--) dryBefore++;
            if (dryBefore < 3 || !Interplay.Ready("worship-rain|" + Gods.HomeKey(map), 2f)) return;
            RainAfterDrought(map, null);
        }

        /// <summary>Rain after the dry days: each couple of two gods who are together now bickers over whose god sent it (once in a few days a couple).</summary>
        public static void RainAfterDrought(Map map, God sent)
        {
            if (map == null) return;
            try
            {
                foreach (var a in map.mapPawns.FreeColonistsSpawned.ToList())
                {
                    if (!Worship.Ours(a)) continue;
                    var b = LovePartnerRelationUtility.ExistingMostLikedLovePartner(a, false);
                    if (b == null || b.thingIDNumber < a.thingIDNumber || !b.Spawned || b.Map != map || !Worship.Ours(b)) continue;
                    var ga = Worship.GodOf(a);
                    var gb = Worship.GodOf(b);
                    if (ga == null || gb == null || ga == gb || !Together.Can(a, b)) continue;
                    if (!Interplay.Ready("worship-bicker|" + a.thingIDNumber + "|" + b.thingIDNumber, WorshipMath.BickerGapDays)) continue;
                    Bicker(a, b, ga, gb, sent);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: couples bickering", e);
            }
        }

        private static void Bicker(Pawn a, Pawn b, God ga, God gb, God sent)
        {
            // together: RainAfterDrought found them side by side (Together.Can), and Speech.Say checks it again.
            string tag = sent == ga ? "mine" : sent == gb ? "theirs" : null;
            if (!Worship.Say(a, "Rimfolk_Pool_Worship_Bicker", ga, b, tag)) return;
            string reply = Worship.Line("Rimfolk_Pool_Worship_BickerReply", b, gb, a, tag == "mine" ? "theirs" : tag == "theirs" ? "mine" : null);
            if (!reply.NullOrEmpty()) Speech.Say(b, reply, a);
            Speech.Logged(a, b, "CC_Wsh_BickerLog".Translate(a.LabelShort.Named("A"), b.LabelShort.Named("B"), Worship.Called(ga).Named("GODA"), Worship.Called(gb).Named("GODB")).Resolve(), false);
            CouncilGameComponent.GiveThought(a, WorshipDefOf.Rimfolk_Worship_Bickered, b);
            CouncilGameComponent.GiveThought(b, WorshipDefOf.Rimfolk_Worship_Bickered, a);
        }
    }
}
