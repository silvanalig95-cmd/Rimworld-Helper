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
    /// The gods in diplomacy (docs/design/gods.md, stage 2, brainstorm item 17):
    /// <list type="bullet">
    /// <item>Peoples who share a god get on better: once a season, a people whose god is one of the colony's own, or one the
    /// colony stands pleased with, thinks a little better of it; a people whose god is the rival of a patron that favors the
    /// colony thinks a little worse (a feud that is really the gods'), never below its own floor.</item>
    /// <item>A holy war: a friend at war with a people whose god is their god's rival asks the colony to stand with them,
    /// to send help, or to stay out.</item>
    /// <item>A temple's envoy: a people comes asking for a relic the colony holds that is sacred to their god (the god's
    /// relic, or their own faith's relic with Ideology). Given, they walk to it and take it home; prayed over, they kneel
    /// by it an hour; refused, they go.</item>
    /// </list>
    /// </summary>
    public static class GodPeoples
    {
        public static void Daily(GodWorldBook b, int now)
        {
            if (b.Gap("shared", GodWorldMath.SharedEveryDays))
            {
                b.Came("shared");
                GodWorld.Safe("shared gods", Shared);
            }
            GodWorld.Safe("a holy war", () => HolyWar(b));
            GodWorld.Safe("a temple's envoy", () => Envoy(b));
        }

        // ================= Shared gods, and a feud that is the gods' =================

        internal static void Shared()
        {
            var home = Homes.Seat;
            if (home == null) return;
            var ours = Gods.ColonyGods(home);
            var patron = Gods.Patron(home);
            var patronWord = patron != null ? Gods.Favour(patron, home) : FavourWord.Unknown;
            foreach (var f in Find.FactionManager.AllFactionsListForReading.ToList())
            {
                if (!Arrivals.Usable(f) || !f.HasGoodwill) continue;
                var theirs = Gods.Of(f).Where(Gods.Known).ToList();
                if (theirs.Count == 0) continue;
                // The god they share with the colony that it stands best with.
                God shared = null;
                var best = FavourWord.Wrathful;
                foreach (var g in theirs)
                {
                    var w = Gods.Favour(g, home);
                    if (!ours.Contains(g) && w < FavourWord.Pleased) continue;
                    if (shared == null || w > best)
                    {
                        shared = g;
                        best = ours.Contains(g) && w < FavourWord.Noticed ? FavourWord.Noticed : w;
                    }
                }
                int up = shared != null ? GodWorldMath.SharedGoodwill(best) : 0;
                if (up > 0 && f.PlayerGoodwill < 80)
                {
                    StoryChains.Goodwill(f, up);
                    ColonyCouncil.Tell.Minor(NewsKind.World, Importance.Flavor, home, GodWorld.T("CC_GW_Shared", shared, f.Name.Named("PEOPLE")), null);
                    continue;
                }
                if (patron == null || theirs.Contains(patron)) continue;
                var rival = theirs.FirstOrDefault(g => Gods.Rivals(patron).Contains(g));
                int down = rival != null ? GodWorldMath.FeudGoodwill(patronWord) : 0;
                if (down < 0 && f.PlayerGoodwill + down >= GodWorldMath.FeudFloor && !f.HostileTo(Faction.OfPlayer))
                {
                    StoryChains.Goodwill(f, down);
                    ColonyCouncil.Tell.Minor(NewsKind.World, Importance.Flavor, home,
                        GodWorld.T("CC_GW_Feud", rival, f.Name.Named("PEOPLE"), patron.name.Named("PATRON")), null);
                }
            }
        }

        // ================= A debt kept or broken =================

        /// <summary>
        /// A debt of honour closed (Diplomacy/FavoursOwed.cs, Close): one the colony owed, kept or broken, moves the standing
        /// of the god who watches debts (its ways: Ducat), unseen; someone may say a word about it. Debts owed to the colony
        /// are the other people's honour, not the colony's, and move nothing here.
        /// </summary>
        public static void DebtClosed(bool colonyOwed, bool kept, Faction people, Map home)
        {
            if (!GodWorld.On || !colonyOwed) return;
            GodWorld.Safe("a debt closed", () =>
            {
                var map = GodWorld.HomeOr(home);
                foreach (var g in GodWorld.Known.Where(GodWays.Debts).ToList())
                {
                    Gods.AddFavour(g, map, kept ? GodWorldMath.DebtKept : GodWorldMath.DebtBroken,
                        GodWorld.T(kept ? "CC_GW_Debt_WhyKept" : "CC_GW_Debt_WhyBroken", g, (people?.Name ?? "?").Named("PEOPLE")), FavourSource.Deed,
                        kept ? GodOffence.None : GodOffence.BrokenWord);
                    GodWorld.Remark(map, IntVec3.Invalid, kept ? "debtpaid" : "debtbroken", g);
                }
            });
        }

        // ================= A holy war =================

        /// <summary>A friend at war with a people whose god is their god's rival, and the two gods: the asker's and the enemy's.</summary>
        private static bool HolyWarOf(Faction f, out Faction enemy, out God ours, out God theirs)
        {
            enemy = null;
            ours = theirs = null;
            if (!Arrivals.Usable(f) || !f.HasGoodwill || f.PlayerGoodwill < 25) return false;
            foreach (var e in Find.FactionManager.AllFactionsListForReading)
            {
                if (e == null || e == f || e.IsPlayer || e.defeated || e.Hidden || !e.def.humanlikeFaction || !f.HostileTo(e)) continue;
                foreach (var g in Gods.Of(f))
                {
                    var r = Gods.Of(e).FirstOrDefault(x => Gods.Rivals(g).Contains(x));
                    if (r == null) continue;
                    enemy = e;
                    ours = g;
                    theirs = r;
                    return true;
                }
            }
            return false;
        }

        internal static void HolyWar(GodWorldBook b)
        {
            if (!GodWorld.CanAsk || !b.Gap("holywar", GodWorldMath.HolyWarGap) || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.HolyWarDays, GodWorld.Factor()))) return;
            var home = Homes.Seat;
            if (home == null) return;
            foreach (var f in Find.FactionManager.AllFactionsListForReading.InRandomOrder().ToList())
            {
                if (!HolyWarOf(f, out var enemy, out var ours, out var theirs)) continue;
                Gods.Learn(ours, "letter");
                Gods.Learn(theirs, "letter");
                int silver = GodWorldMath.Gift(GodWorld.Wealth(home), GodWorldMath.HolyWarSilver);
                string text = GodWorld.T("CC_GW_HolyWar_Text", ours, f.Name.Named("PEOPLE"), enemy.Name.Named("ENEMY"), theirs.name.Named("ENEMYGOD"),
                    (theirs.epithet ?? "").Named("ENEMYEPITHET"), silver.Named("COUNT"));
                if (GodAsks.Ask(GodAsks.HolyWar, home, ours, GodWorld.T("CC_GW_HolyWar_Label", ours, f.Name.Named("PEOPLE")), text, null, theirs, f, null, silver,
                        enemy.loadID.ToString(), -1, 3f) == null)
                    return;
                b.Came("holywar");
                GodWorld.Remember(ours, home, GodActKind.Act,
                    GodWorld.T("CC_GW_Told_HolyWarAsk", ours, f.Name.Named("PEOPLE"), enemy.Name.Named("ENEMY"), theirs.name.Named("ENEMYGOD")), theirs);
                return;
            }
        }

        private static Faction EnemyOf(ChoiceLetter_GodWorld l) =>
            int.TryParse(l.what, out int id) ? Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => x.loadID == id) : null;

        public static IEnumerable<GodAnswer> HolyWarAnswers(ChoiceLetter_GodWorld l)
        {
            var f = l.people;
            var e = EnemyOf(l);
            var map = l.HomeMap;
            if (f == null || e == null || map == null || !Arrivals.Usable(f) || !f.HostileTo(e)) yield break;
            yield return new GodAnswer("CC_GW_HolyWar_Stand".Translate(f.Name.Named("PEOPLE")).Resolve(), Stand);
            int have = PlayCarry.Have(map, "silver", ThingDefOf.Silver);
            yield return new GodAnswer("CC_GW_HolyWar_Help".Translate(l.count.Named("COUNT")).Resolve(), Help,
                have < l.count ? "CC_Dilemma_NotEnough".Translate(l.count, ThingDefOf.Silver.label).ToString() : null);
            yield return new GodAnswer("CC_GW_HolyWar_Out".Translate().Resolve(), StayOut);
        }

        private static void Stand(ChoiceLetter_GodWorld l)
        {
            var f = l.people;
            var e = EnemyOf(l);
            var map = l.HomeMap;
            if (f == null) return;
            StoryChains.Goodwill(f, GodWorldMath.HolyWarStand);
            FactionMemory.Note(f, "sidedWith", e?.Name);
            if (e != null && e.HasGoodwill && !e.HostileTo(Faction.OfPlayer))
            {
                StoryChains.Goodwill(e, GodWorldMath.HolyWarEnemy);
                FactionMemory.Note(e, "tookSides", f.Name);
            }
            if (l.God != null) Gods.AddFavour(l.God, map, GodWorldMath.HolyWarFavour, GodWorld.T("CC_GW_HolyWar_Why", l.God, f.Name.Named("PEOPLE")), FavourSource.Deed);
            if (l.Other != null) Gods.AddFavour(l.Other, map, -GodWorldMath.HolyWarFavour, GodWorld.T("CC_GW_HolyWar_WhyAgainst", l.Other, f.Name.Named("PEOPLE")), FavourSource.Deed);
            GodWorld.Chronicle(GodWorld.T("CC_GW_HolyWar_Chronicle", l.God, f.Name.Named("PEOPLE"), (e?.Name ?? "?").Named("ENEMY")));
        }

        private static void Help(ChoiceLetter_GodWorld l)
        {
            var f = l.people;
            var map = l.HomeMap;
            if (f == null || map == null) return;
            var ctx = new DilemmaContext { map = map, faction = f };
            // apart: the silver goes out to the gate, where their people's courier takes it to the war.
            new DilemmaEffect_CarryOut { what = "silver", count = l.count, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = "CC_GW_HolyWar_HelpLabel".Translate().Resolve() }
                .Apply(ctx);
            StoryChains.Goodwill(f, GodWorldMath.HolyWarHelp);
            FactionMemory.Note(f, "gaveGift", l.God?.name);
            if (l.God != null) Gods.AddFavour(l.God, map, GodWorldMath.HolyWarHelpFavour, GodWorld.T("CC_GW_HolyWar_WhyHelp", l.God, f.Name.Named("PEOPLE")), FavourSource.Offering);
        }

        private static void StayOut(ChoiceLetter_GodWorld l)
        {
            var f = l.people;
            if (f == null) return;
            StoryChains.Goodwill(f, GodWorldMath.HolyWarOut);
        }

        // ================= A temple's envoy and a relic =================

        /// <summary>A relic the colony holds on a home, the god it is sacred to, and the people who would ask for it.</summary>
        private struct RelicAsk
        {
            public Thing relic;
            public God god;
            public Faction people;
            public Map map;
        }

        private static RelicAsk? FindAsk()
        {
            foreach (var (thing, god, ideo, map) in GodWorldGifts.Relics())
            {
                // Their faith's own relic (Ideology), or a relic of a god they hold.
                var f = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x =>
                    Arrivals.Usable(x) && x.HasGoodwill && x.PlayerGoodwill >= 0 && (ideo != null && x.ideos?.PrimaryIdeo == ideo || god != null && god.factions.Contains(x)));
                if (f == null) continue;
                var g = god ?? Gods.Of(f).FirstOrDefault();
                if (g == null) continue;
                return new RelicAsk { relic = thing, god = g, people = f, map = map };
            }
            return null;
        }

        internal static void Envoy(GodWorldBook b)
        {
            if (!GodWorld.CanAsk || !b.Gap("envoy", GodWorldMath.EnvoyGap) || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.EnvoyDays, GodWorld.Lean()))) return;
            var ask = FindAsk();
            if (ask == null) return;
            var a = ask.Value;
            var kind = Arrivals.PersonKind(a.people);
            if (kind == null) return;
            var envoy = Arrivals.Make(kind, a.people, new FloatRange(35f, 65f));
            if (envoy == null) return;
            if (Arrivals.Visit(new List<Pawn> { envoy }, a.map, a.people, null, 14f) == null)
            {
                Arrivals.Discard(envoy);
                return;
            }
            Gods.Learn(a.god, "visitors");
            var d = b.Add(new GodDoing { kind = "relic", god = a.god.id, home = Homes.KeyOf(a.map), pawn = envoy, faction = a.people, thing = a.relic, until = GodWorld.Now + GenDate.TicksPerDay });
            string text = GodWorld.T("CC_GW_Envoy_Text", a.god, a.people.Name.Named("PEOPLE"), envoy.Named("ENVOY"), a.relic.LabelShort.Named("RELIC"));
            if (GodAsks.Ask(GodAsks.Envoy, a.map, a.god, GodWorld.T("CC_GW_Envoy_Label", a.god, a.people.Name.Named("PEOPLE")), text, new LookTargets(envoy), null, a.people,
                    envoy, 0, a.relic.def.defName, d.id, 1f) == null)
                return;
            b.Came("envoy");
            GodWorld.Remember(a.god, a.map, GodActKind.Act, GodWorld.T("CC_GW_Told_Envoy", a.god, envoy.Named("ENVOY"), a.people.Name.Named("PEOPLE"), a.relic.LabelShort.Named("RELIC")));
        }

        public static IEnumerable<GodAnswer> EnvoyAnswers(ChoiceLetter_GodWorld l)
        {
            var d = l.Doing;
            if (d == null || d.stage != 0 || d.thing == null || d.thing.Destroyed || !PeoplesG.Up(d.pawn, d.Map)) yield break;
            string relic = d.thing.LabelShort;
            yield return new GodAnswer("CC_GW_Envoy_Give".Translate(relic.Named("RELIC")).Resolve(), x => Answer(x, 1));
            yield return new GodAnswer("CC_GW_Envoy_Pray".Translate(relic.Named("RELIC")).Resolve(), x => Answer(x, 2));
            yield return new GodAnswer("CC_GW_Envoy_Refuse".Translate().Resolve(), x => Answer(x, 3));
        }

        private static void Answer(ChoiceLetter_GodWorld l, int how)
        {
            var d = l.Doing;
            if (d == null) return;
            var g = Gods.ById(d.god);
            var map = d.Map;
            if (how == 3)
            {
                d.done = true;
                if (d.faction != null) StoryChains.Goodwill(d.faction, GodWorldMath.RelicRefused);
                if (d.faction != null) FactionMemory.Note(d.faction, "refused", d.thing?.LabelShort);
                if (g != null) Gods.AddFavour(g, map, GodWorldMath.RelicRefusedFavour, GodWorld.T("CC_GW_Envoy_WhyRefused", g, (d.thing?.LabelShort ?? "?").Named("RELIC")));
                Arrivals.SendAway(new[] { d.pawn });
                return;
            }
            d.stage = how;
            d.until = GodWorld.Now + GenDate.TicksPerDay / 2;
            Walk(d);
        }

        /// <summary>Where the relic is: on the ground, or the cell of what holds it (a reliquary, a shelf, a colonist's pack).</summary>
        private static IntVec3 RelicCell(Thing t, Map map)
        {
            if (t == null || t.Destroyed) return IntVec3.Invalid;
            if (t.Spawned && t.Map == map) return t.Position;
            var holder = t.ParentHolder;
            while (holder != null && !(holder is Thing))
                holder = holder.ParentHolder;
            return holder is Thing h && h.Spawned && h.Map == map ? h.Position : t.PositionHeld;
        }

        /// <summary>The envoy walks to the relic (their own feet; their lord keeps them on the map).</summary>
        private static void Walk(GodDoing d)
        {
            var map = d.Map;
            var cell = RelicCell(d.thing, map);
            if (!PeoplesG.Up(d.pawn, map) || !cell.IsValid) return;
            try
            {
                var job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                job.locomotionUrgency = LocomotionUrgency.Walk;
                d.pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: the envoy to the relic", e);
            }
        }

        /// <summary>The envoy's hour: at the relic they take it (given) or kneel by it a while (prayed over), then they go home.</summary>
        public static void Hour(GodWorldBook b, int now)
        {
            foreach (var d in b.Of("relic").ToList())
            {
                var map = d.Map;
                var g = Gods.ById(d.god);
                if (!PeoplesG.Up(d.pawn, map) || d.thing == null || d.thing.Destroyed || now > d.until)
                {
                    d.done = true;
                    if (PeoplesG.Up(d.pawn, map)) Arrivals.SendAway(new[] { d.pawn });
                    continue;
                }
                if (d.stage == 0) continue;
                var cell = RelicCell(d.thing, map);
                if (!cell.IsValid) continue;
                if (!d.pawn.Position.InHorDistOf(cell, 2.5f))
                {
                    if (d.pawn.CurJobDef != JobDefOf.Goto) Walk(d);
                    continue;
                }
                if (d.stage == 1) Taken(d, g, map);
                else if (d.stage == 2)
                {
                    // Kneeling by it an hour: the keep job faces it; the next hour they rise and go.
                    if (d.count == 0)
                    {
                        d.count = 1;
                        var keep = PeopleWorkOn.KeepJob;
                        if (keep != null)
                        {
                            var job = JobMaker.MakeJob(keep, d.pawn.Position, cell);
                            job.count = GenDate.TicksPerHour;
                            d.pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                        }
                        FleckMaker.ThrowMetaIcon(d.pawn.Position, map, FleckDefOf.Meditating);
                        continue;
                    }
                    Prayed(d, g, map);
                }
            }
        }

        private static void Taken(GodDoing d, God g, Map map)
        {
            d.done = true;
            var relic = d.thing;
            try
            {
                if (relic.Spawned) relic.DeSpawn();
                else relic.holdingOwner?.Remove(relic);
                if (!d.pawn.inventory.innerContainer.TryAdd(relic)) GenPlace.TryPlaceThing(relic, d.pawn.Position, map, ThingPlaceMode.Near);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: the relic handed over", e);
                return;
            }
            if (d.faction != null)
            {
                StoryChains.Goodwill(d.faction, GodWorldMath.RelicGiven);
                FactionMemory.Note(d.faction, "gift", relic.LabelShort);
            }
            if (g != null) Gods.AddFavour(g, map, GodWorldMath.RelicGivenFavour, GodWorld.T("CC_GW_Envoy_WhyGiven", g, relic.LabelShort.Named("RELIC")), FavourSource.Offering);
            GodWorldGifts.Gone(relic);
            ColonyCouncil.Tell.Message(NewsKind.World, Importance.Notable, map,
                GodWorld.T("CC_GW_Envoy_Taken", g, d.pawn.Named("ENVOY"), relic.LabelShort.Named("RELIC"), (d.faction?.Name ?? "?").Named("PEOPLE")), new LookTargets(d.pawn),
                MessageTypeDefOf.NeutralEvent);
            GodWorld.Chronicle(GodWorld.T("CC_GW_Envoy_Chronicle", g, relic.LabelShort.Named("RELIC"), (d.faction?.Name ?? "?").Named("PEOPLE")));
            Arrivals.SendAway(new[] { d.pawn });
        }

        private static void Prayed(GodDoing d, God g, Map map)
        {
            d.done = true;
            if (d.faction != null) StoryChains.Goodwill(d.faction, GodWorldMath.RelicLent);
            if (g != null) Gods.AddFavour(g, map, GodWorldMath.RelicGivenFavour * 0.4f, GodWorld.T("CC_GW_Envoy_WhyPrayed", g, (d.thing?.LabelShort ?? "?").Named("RELIC")), FavourSource.Rite);
            ColonyCouncil.Tell.Minor(NewsKind.World, Importance.Flavor, map,
                GodWorld.T("CC_GW_Envoy_PrayedNews", g, d.pawn.Named("ENVOY"), (d.thing?.LabelShort ?? "?").Named("RELIC")), new LookTargets(d.pawn));
            Arrivals.SendAway(new[] { d.pawn });
        }
    }
}
