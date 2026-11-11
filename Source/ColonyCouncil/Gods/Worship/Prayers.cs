using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Prayer in hard moments (docs/design/gods.md, "Worship": prayer; brainstorm 13). The region's hour looks for them: someone
    /// gravely ill (they pray in bed, and those close to them pray too, to the god of healing where they have one), someone
    /// of the colony dead (the grieving, to the god of the dead), raiders at the gate (those not under arms, a word where
    /// they stand, to the god of war), and a drought in the growing season (the growers, to the god of the harvest or the
    /// storm). Who prays: the devout most, the wavering little, a scoffer never (WorshipMath.PrayChance). It plays out on the
    /// map: in their free time they walk to the god's place and kneel there a while, or kneel where they stand; the sick
    /// and those in a fight say it over their heads where they are. A prayer comforts (strain eased in the heart's daily cap,
    /// a small moment of hope) and pleases the god a little. Sometimes the god answers at the step's strength (never in
    /// Believed): a fever eased a little at night, the grieving consoled and the lost in their dreams, a fighter's nerve, rain.
    /// A god answers at most once in five days at a home, through the presence part's moment or a plain card. Later, a
    /// prayer comes good on its own (the sick get well, the rain comes, nobody falls in the raid) and the god is credited;
    /// or it goes unanswered (the sick die, the rain never comes, someone falls), and that can be a crisis of faith
    /// (Worship/FaithHeart.cs). The Hunger is never answered here: it hears the desperate on its own terms, through the
    /// world part's bargain (Gods/World/GodVoid.cs).
    /// </summary>
    public static class Prayers
    {
        /// <summary>An unsaid prayer waits this many hours for them to be free, then is let go.</summary>
        public const int WaitHours = 12;

        /// <summary>The god of an open prayer of theirs (for the job's report).</summary>
        public static God GodFor(Pawn p)
        {
            var r = Worship.Book?.prayers.LastOrDefault(x => x != null && x.pawn == p && !x.said);
            return r != null ? Gods.ById(r.god) : Worship.GodOf(p);
        }

        // ================= The hour =================

        public static void Hourly(WorshipBook b, Map map, int now)
        {
            int key = Gods.HomeKey(map);
            int today = b.prayers.Count(r => r != null && r.home == key && now - r.tick < GenDate.TicksPerDay);
            if (today < WorshipMath.PrayersADay)
            {
                Sickness(b, map, now, ref today);
                Deaths(b, map, now, ref today);
                Raid(b, map, now, ref today);
                Drought(b, map, now, ref today);
            }
            foreach (var r in b.prayers.Where(x => x != null && x.home == key && !x.settled).ToList())
            {
                try
                {
                    if (!r.said) TrySay(b, r, map, now);
                    else Settle(b, r, map, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("worship: a prayer", e);
                }
            }
            RainComing(b, map, now);
        }

        private static bool Recently(WorshipBook b, Pawn p, PrayerKind k, int now) =>
            b.prayers.Any(r => r != null && r.pawn == p && r.kind == k && now - r.tick < WorshipMath.PrayGapDays * GenDate.TicksPerDay);

        private static GodDomain DomainOf(PrayerKind k) => k switch
        {
            PrayerKind.Sickness => GodDomain.Healing,
            PrayerKind.Death => GodDomain.Dead,
            PrayerKind.Raid => GodDomain.War,
            _ => GodDomain.Harvest
        };

        /// <summary>Someone of the colony who might pray now: up to it, not a baby, with a god, and by the roll.</summary>
        private static bool Prays(WorshipBook b, Pawn p, PrayerKind k, bool close, int now)
        {
            if (!Worship.Ours(p) || p.DevelopmentalStage.Baby() || Recently(b, p, k, now)) return false;
            if (Worship.GodOf(p, DomainOf(k)) == null) return false;
            return Rand.Chance(WorshipMath.PrayChance(Worship.Devout(p), Worship.Wavering(p), Worship.Scoffer(p), close));
        }

        private static PrayerRecord Add(WorshipBook b, Pawn p, PrayerKind k, Pawn about, Map map, int now)
        {
            var g = Worship.GodOf(p, DomainOf(k));
            if (k == PrayerKind.Drought && g != null && g.domain != GodDomain.Harvest && g.second != GodDomain.Harvest)
                g = Worship.GodOf(p, GodDomain.Storm) ?? g;
            if (g == null) return null;
            var r = new PrayerRecord { pawn = p, god = g.id, kind = k, about = about, aboutName = about?.LabelShort, home = Gods.HomeKey(map), tick = now };
            b.prayers.Add(r);
            return r;
        }

        // ---------------- The hard moments ----------------

        /// <summary>A sickness that can kill, as the game marks one (a sick thought and a lethal severity), now visible.</summary>
        public static Hediff Grave(Pawn p)
        {
            var hs = p?.health?.hediffSet;
            if (hs == null) return null;
            foreach (var h in hs.hediffs)
                if (h != null && h.Visible && h.def.makesSickThought && h.def.lethalSeverity > 0f && h.Severity > 0.15f * h.def.lethalSeverity) return h;
            return null;
        }

        /// <summary>Close to them: a partner, kin by blood, or a friend (opinion 30 or more).</summary>
        public static bool Close(Pawn a, Pawn b)
        {
            if (a?.relations == null || b == null || a == b) return false;
            if (LovePartnerRelationUtility.LovePartnerRelationExists(a, b)) return true;
            if (a.GetRelations(b).Any(r => r.familyByBloodRelation)) return true;
            return a.relations.OpinionOf(b) >= 30;
        }

        private static void Sickness(WorshipBook b, Map map, int now, ref int today)
        {
            foreach (var sick in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (today >= WorshipMath.PrayersADay) return;
                if (Grave(sick) == null) continue;
                // The sick themselves, where they lie.
                if (!sick.Downed || sick.Awake())
                    if (Prays(b, sick, PrayerKind.Sickness, true, now) && Add(b, sick, PrayerKind.Sickness, sick, map, now) != null)
                    {
                        today++;
                        Vows.MaybeNoMeat(b, sick, sick, map);
                    }
                foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
                {
                    if (today >= WorshipMath.PrayersADay) return;
                    if (p == sick || !Close(p, sick) || !Prays(b, p, PrayerKind.Sickness, true, now)) continue;
                    if (Add(b, p, PrayerKind.Sickness, sick, map, now) == null) continue;
                    today++;
                    // Sometimes the prayer becomes a vow: no meat until they are well.
                    Vows.MaybeNoMeat(b, p, sick, map);
                }
            }
        }

        private static void Deaths(WorshipBook b, Map map, int now, ref int today)
        {
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (today >= WorshipMath.PrayersADay) return;
                var lost = Lost(p);
                if (lost == null) continue;
                bool close = Close(p, lost);
                if (!Prays(b, p, PrayerKind.Death, close, now)) continue;
                if (Add(b, p, PrayerKind.Death, lost, map, now) != null) today++;
            }
        }

        /// <summary>One of the colony's own who died in the last day, by their memories of it, or null.</summary>
        private static Pawn Lost(Pawn p)
        {
            var mems = p.needs?.mood?.thoughts?.memories?.Memories;
            if (mems == null) return null;
            foreach (var m in mems)
            {
                if (m?.otherPawn == null || !m.otherPawn.Dead || m.age > GenDate.TicksPerDay) continue;
                if (m.def == ThoughtDefOf.KnowColonistDied || m.def == ThoughtDefOf.PawnWithGoodOpinionDied || m.MoodOffset() < -4f && m.otherPawn.Faction == Faction.OfPlayer)
                    return m.otherPawn;
            }
            return null;
        }

        private static void Raid(WorshipBook b, Map map, int now, ref int today)
        {
            int last = map.storyState?.LastThreatBigTick ?? -1;
            if (last <= 0 || now - last > GenDate.TicksPerHour * 3) return;
            if (!GenHostility.AnyHostileActiveThreatToPlayer(map, false, false)) return;
            int said = 0;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
            {
                if (said >= 3 || today >= WorshipMath.PrayersADay + 2) return;
                if (p.Drafted || p.Downed || p.InMentalState || !p.Awake() || p.DevelopmentalStage.Baby()) continue;
                if (!Prays(b, p, PrayerKind.Raid, false, now)) continue;
                var r = Add(b, p, PrayerKind.Raid, null, map, now);
                if (r == null) continue;
                // A word where they stand, with the raiders at the gate: no kneeling now.
                SayAndMark(b, r, p, null);
                said++;
                today++;
            }
        }

        private static void Drought(WorshipBook b, Map map, int now, ref int today)
        {
            if (!InDrought(map)) return;
            var growers = map.mapPawns.FreeColonistsSpawned.Where(p => !p.WorkTypeIsDisabled(WorkTypeDefOf.Growing))
                .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Plants)?.Level ?? 0).Take(3).ToList();
            foreach (var p in growers)
            {
                if (today >= WorshipMath.PrayersADay) return;
                if (!Prays(b, p, PrayerKind.Drought, false, now)) continue;
                if (Add(b, p, PrayerKind.Drought, null, map, now) != null) today++;
            }
        }

        /// <summary>A drought on a home: dry days running in the growing season, with a field sown.</summary>
        public static bool InDrought(Map map)
        {
            var book = WeatherBook.Of(map);
            if (book?.DaysBefore == null || book.Today == null) return false;
            WeatherMath.RunsWithToday(book.DaysBefore, book.Today, out _, out int dry, out _);
            bool growing = map.mapTemperature.OutdoorTemp > 6f && GenLocalDate.Season(map) != Season.Winter;
            bool fields = map.zoneManager.AllZones.Any(z => z is Zone_Growing);
            return WorshipMath.Drought(dry, growing, fields);
        }

        // ---------------- Saying it ----------------

        private static void TrySay(WorshipBook b, PrayerRecord r, Map map, int now)
        {
            var p = r.pawn;
            if (p == null || p.Dead || now - r.tick > WaitHours * GenDate.TicksPerHour)
            {
                r.said = true;
                r.settled = true;
                return;
            }
            if (!p.Spawned || p.Map != map) return;
            // The sick in bed say it where they lie.
            if (p.InBed() || p.Downed)
            {
                if (p.Awake() && !p.InMentalState) SayAndMark(b, r, p, null);
                return;
            }
            if (!WorshipJobs.Free(p)) return;
            var g = Gods.ById(r.god);
            var shrine = Shrines.Nearest(p, g);
            if (shrine != null && shrine.Position.DistanceTo(p.Position) > 90f) shrine = null;
            var job = shrine != null ? WorshipJobs.At(WorshipJobs.PrayJob, shrine, Rand.Range(600, 900)) : WorshipJobs.At(WorshipJobs.PrayJob, p.Position, Rand.Range(300, 500));
            WorshipJobs.Start(p, job);
        }

        /// <summary>A prayer said over their head where they are (in bed, with raiders at the gate): said, and its comfort.</summary>
        private static void SayAndMark(WorshipBook b, PrayerRecord r, Pawn p, Thing shrine)
        {
            var g = Gods.ById(r.god);
            Worship.Say(p, "Rimfolk_Pool_Worship_Pray", g, null, "pray:" + r.kind.ToString().ToLowerInvariant(), p.InBed() ? "inbed" : null);
            Said(b, r, p, shrine);
        }

        /// <summary>They have knelt at the place (or where they stand): the words, over their head.</summary>
        public static void Arrive(Pawn p, Thing shrine)
        {
            var b = Worship.Book;
            var r = b?.prayers.LastOrDefault(x => x != null && x.pawn == p && !x.said);
            var g = r != null ? Gods.ById(r.god) : Worship.GodOf(p);
            Worship.Say(p, "Rimfolk_Pool_Worship_Pray", g, null, r != null ? "pray:" + r.kind.ToString().ToLowerInvariant() : null, shrine != null ? "atshrine" : null);
            if (shrine != null) Worship.PriestLight(p, shrine.Position);
        }

        /// <summary>The prayer is said (the job done).</summary>
        public static void Prayed(Pawn p, Thing shrine)
        {
            var b = Worship.Book;
            var r = b?.prayers.LastOrDefault(x => x != null && x.pawn == p && !x.said);
            if (r == null) return;
            Said(b, r, p, shrine);
        }

        private static void Said(WorshipBook b, PrayerRecord r, Pawn p, Thing shrine)
        {
            r.said = true;
            r.atShrine = shrine != null;
            var map = p.MapHeld;
            var g = Gods.ById(r.god);
            if (g == null) return;
            bool devout = Worship.Devout(p);
            Interplay.Ease(p, WorshipMath.Comfort(r.atShrine, devout), StrainRelief.Quiet);
            CouncilGameComponent.GiveThought(p, WorshipDefOf.Rimfolk_Worship_Prayed);
            Worship.Favour(g, map, WorshipMath.PrayerFavour(r.atShrine) * (Worship.IsPriest(p) ? WorshipMath.PriestRiteFactor : 1f),
                "CC_Wsh_Why_Prayer".Translate(p.LabelShort.Named("PAWN")).Resolve(), FavourSource.Rite);
            TryAnswer(b, r, p, g, map);
        }

        // ================= An answer =================

        private static void TryAnswer(WorshipBook b, PrayerRecord r, Pawn p, God g, Map map)
        {
            if (map == null || !Homes.IsHome(map)) return;
            int key = Gods.HomeKey(map);
            int now = Worship.Now;
            var last = b.Mark(key, "answer", g.id);
            if (last != null && now - last.value < WorshipMath.AnswerGapDays * GenDate.TicksPerDay) return;
            bool priest = Worship.IsPriest(p) || Worship.Priest(map) != null && r.atShrine && Worship.Priest(map).Position.InHorDistOf(p.Position, 10f);
            // A god worship doesn't move answers nobody (Helios gives to all alike); one that returns by night hears the night's prayers better (Selene).
            // The Hunger answers only through its own bargain with the desperate (Gods/World/GodVoid.cs), never a prayer here.
            if (!GodWorshipDef.Moved(g) || g.kind == GodKind.Hunger || g.baseKind == GodKind.Hunger) return;
            float chance = WorshipMath.AnswerChance(Gods.Realness, g.kind, Gods.Loudness(g), Gods.Favour(g, map), priest, r.atShrine, Worship.IsHonoured(g, map));
            int hour = GenLocalDate.HourOfDay(map);
            if (hour >= 20 || hour < 5) chance = Mathf.Min(0.6f, chance * (GodWorshipDef.For(g)?.nightFactor ?? 1f));
            if (!Rand.Chance(chance)) return;
            if (!Answer(r, p, g, map)) return;
            r.answered = true;
            b.SetMark(key, "answer", g.id, now, now);
            CouncilGameComponent.GiveThought(p, WorshipDefOf.Rimfolk_Worship_Answered);
            FaithHeart.Witnessed(map, g, p.Position);
        }

        private static string Step => Worship.Step;

        /// <summary>
        /// The god's answer, at the step's strength, shown on the map as what happened after the prayer; the god behind it named
        /// only as the core allows (Worship.Sign). False when there was nothing to answer after all.
        /// </summary>
        private static bool Answer(PrayerRecord r, Pawn p, God g, Map map)
        {
            string title = "CC_Wsh_Answer_Title".Translate().Resolve();
            switch (r.kind)
            {
                case PrayerKind.Sickness:
                {
                    var sick = r.about ?? p;
                    var h = Grave(sick);
                    if (h == null || sick.Dead) return false;
                    h.Severity = Mathf.Max(0.01f, h.Severity - WorshipMath.SicknessEase(Gods.Realness) * h.def.lethalSeverity);
                    var imm = sick.health?.immunity?.GetImmunityRecord(h.def);
                    if (imm != null) imm.immunity = Mathf.Min(1f, imm.immunity + WorshipMath.SicknessEase(Gods.Realness) * 0.6f);
                    string text = ("CC_Wsh_Answer_Sickness_" + Step).Translate(sick.LabelShort.Named("SICK"), Worship.Called(g).Named("GOD"), h.LabelBase.Named("ILLNESS"),
                        p.LabelShort.Named("PAWN")).Resolve();
                    Worship.Sign(g, map, sick.PositionHeld, PresenceKind.Blessing, title, text, sick, Gods.Realness == GodRealness.Walking, GodActKind.Blessing);
                    return true;
                }
                case PrayerKind.Death:
                {
                    Interplay.Ease(p, 8f, StrainRelief.Quiet);
                    GodDreams.QueueLost(p, g, r.aboutName);
                    string text = ("CC_Wsh_Answer_Death_" + Step).Translate(p.LabelShort.Named("PAWN"), Worship.Called(g).Named("GOD"), (r.aboutName ?? "?").Named("DEAD")).Resolve();
                    Worship.Sign(g, map, p.Position, PresenceKind.Act, title, text, p, false);
                    return true;
                }
                case PrayerKind.Raid:
                {
                    var frenzy = DefDatabase<InspirationDef>.GetNamedSilentFail("Frenzy_Shoot");
                    if (frenzy == null || p.WorkTagIsDisabled(WorkTags.Violent) || p.Inspired || p.mindState?.inspirationHandler == null) return false;
                    if (!p.mindState.inspirationHandler.TryStartInspiration(frenzy, "CC_Wsh_Answer_RaidReason".Translate(p.LabelShort.Named("PAWN"), Worship.Called(g).Named("GOD")).Resolve())) return false;
                    string text = ("CC_Wsh_Answer_Raid_" + Step).Translate(p.LabelShort.Named("PAWN"), Worship.Called(g).Named("GOD")).Resolve();
                    Worship.Sign(g, map, p.Position, PresenceKind.Blessing, title, text, p, false, GodActKind.Blessing);
                    return true;
                }
                default:
                {
                    // Rain: at once when the gods walk, with the evening otherwise.
                    var b = Worship.Book;
                    int when = Worship.Now + (Gods.Realness == GodRealness.Walking ? GenDate.TicksPerHour : Rand.Range(6, 14) * GenDate.TicksPerHour);
                    b.SetMark(Gods.HomeKey(map), "rain", g.id, when, when);
                    return true;
                }
            }
        }

        /// <summary>Rain an answered prayer brought: it comes at its hour, with the god's sign.</summary>
        private static void RainComing(WorshipBook b, Map map, int now)
        {
            var m = b.Mark(Gods.HomeKey(map), "rain");
            if (m == null || m.value <= 0 || now < m.value) return;
            b.homeMarks.Remove(m);
            var g = Gods.ById(m.god);
            if (g == null) return;
            var rain = DefDatabase<WeatherDef>.GetNamedSilentFail(Gods.Realness == GodRealness.Walking ? "RainyThunderstorm" : "Rain") ?? DefDatabase<WeatherDef>.GetNamedSilentFail("Rain");
            if (rain == null || map.weatherManager.curWeather == rain) return;
            map.weatherManager.TransitionTo(rain);
            string text = ("CC_Wsh_Answer_Drought_" + Step).Translate(Worship.Called(g).Named("GOD")).Resolve();
            Worship.Sign(g, map, map.Center, PresenceKind.Blessing, "CC_Wsh_Answer_Title".Translate().Resolve(), text, null,
                Gods.Realness == GodRealness.Walking, GodActKind.Blessing);
            FaithHeart.RainAfterDrought(map, g);
        }

        // ================= Settled: credited, or unanswered =================

        private static void Settle(WorshipBook b, PrayerRecord r, Map map, int now)
        {
            var g = Gods.ById(r.god);
            var p = r.pawn;
            if (g == null || p == null || p.Dead)
            {
                r.settled = true;
                return;
            }
            bool late = now - r.tick > WorshipMath.ResolveDays(r.kind) * GenDate.TicksPerDay;
            switch (r.kind)
            {
                case PrayerKind.Sickness:
                    if (r.about == null || r.about.Dead)
                    {
                        if (!r.answered) FaithHeart.Unanswered(b, r, p, g, map);
                        r.settled = true;
                    }
                    else if (Grave(r.about) == null)
                    {
                        Credit(r, p, g, map);
                        r.settled = true;
                    }
                    else if (late) r.settled = true;
                    break;
                case PrayerKind.Death:
                    if (late) r.settled = true;
                    break;
                case PrayerKind.Raid:
                    if (GenHostility.AnyHostileActiveThreatToPlayer(map, false, false)) break;
                    if (Lost(p) is Pawn fell && Close(p, fell))
                    {
                        r.about = fell;
                        r.aboutName = fell.LabelShort;
                        FaithHeart.Unanswered(b, r, p, g, map);
                    }
                    else Credit(r, p, g, map);
                    r.settled = true;
                    break;
                default:
                    if (!InDrought(map))
                    {
                        Credit(r, p, g, map);
                        r.settled = true;
                    }
                    else if (late)
                    {
                        FaithHeart.Unanswered(b, r, p, g, map);
                        r.settled = true;
                    }
                    break;
            }
        }

        /// <summary>It came good: the god credited, a word of thanks over their head when they're about, and their small moment.</summary>
        private static void Credit(PrayerRecord r, Pawn p, God g, Map map)
        {
            if (r.answered) return;
            string why = ("CC_Wsh_Why_Credit_" + r.kind).Translate(p.LabelShort.Named("PAWN"), (r.aboutName ?? "").Named("SICK")).Resolve();
            Worship.Favour(g, map, WorshipMath.CreditFavour, why, FavourSource.Rite);
            if (p.Spawned) Worship.Say(p, "Rimfolk_Pool_Worship_Thanks", g, null, "pray:" + r.kind.ToString().ToLowerInvariant());
            // A raid beaten off is the gods of war's own deed already (Gods/GodDeeds.cs): thanks and favour, no mood of its own.
            if (r.kind != PrayerKind.Raid) CouncilGameComponent.GiveThought(p, WorshipDefOf.Rimfolk_Worship_Answered);
            if (Faiths.On) Faiths.Steady(p, 0.03f);
            // The colony's book: what was prayed for and what followed, as the one who prayed tells it (a few days apart a person).
            string words = ("CC_Wsh_Book_Credit_" + r.kind).Translate(Worship.Called(g, p).Named("GOD"), (r.aboutName ?? "").Named("SICK")).Resolve();
            Worship.Witness(g, map, GodActKind.Favour, words, p.PositionHeld);
            if (Interplay.Ready("worship-book|" + p.thingIDNumber, WorshipMath.BookGapDays)) GodLore.Prayed(p, g, words);
        }

        // ================= Dev =================

        /// <summary>Dev: someone prays now, in a moment of this kind (the sick one, if any).</summary>
        public static void DevPray(Pawn p, PrayerKind k)
        {
            var b = Worship.Book;
            if (b == null || p?.Map == null) return;
            Pawn about = k == PrayerKind.Sickness ? p.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(x => Grave(x) != null) ?? p : null;
            var r = Add(b, p, k, about, p.Map, Worship.Now);
            if (r == null) Messages.Message("CC_Wsh_Dev_NoGod".Translate(p.LabelShort), p, MessageTypeDefOf.RejectInput, false);
        }
    }
}
