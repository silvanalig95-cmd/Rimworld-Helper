using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The ones who don't fit, in their own ways (docs/design/gods.md, "The ones who don't fit"; the player: "outliers that
    /// work differently or have really different way of interacting, being"):
    /// <list type="bullet">
    /// <item>An archotech bargains in exact terms and keeps them: so many of a thing at the gate within so many days, for
    /// exactly so much from orbit. The colony delivers as soon as its stores hold them; the reward falls the next day. A term
    /// missed is a broken word, which a cold mind does not forget.</item>
    /// <item>The dead god's mark is found in an old place on the colony's land (an ancient wall, a wreck) by one of the colony
    /// working near it; the few who still keep it hope again.</item>
    /// <item>The fading god, whose people are gone, comes asking to be remembered: a gentle one blesses whoever remembers it,
    /// a bitter one puts the sun out over a colony that will not.</item>
    /// <item>The Sleeper in the Sunken Vault wants out: three asks, each a step; the third opens the door a crack (a psychic
    /// calm over the colony, and the vaultfolk furious).</item>
    /// <item>A newborn god of the colony's own faith gives its first small signs as the colony's devotion grows it.</item>
    /// </list>
    /// </summary>
    public static class GodOutliers
    {
        private static int Now => GodWorld.Now;

        public static void Daily(GodWorldBook b, int now)
        {
            GodWorld.Safe("an archotech's terms", () => Terms(b));
            GodWorld.Safe("terms kept or broken", () => TermsDue(b, now));
            GodWorld.Safe("the dead god's mark", () => DeadMark(b));
            GodWorld.Safe("the fading god", () => Fading(b));
            GodWorld.Safe("the Sleeper", () => Sleeper(b));
            GodWorld.Safe("a newborn's signs", () => Newborn(b));
        }

        // ================= An archotech's exact terms =================

        /// <summary>What an archotech asks, by the colony's means: components from a colony that makes them, else steel, else wood.</summary>
        private static ThingDef Goods() =>
            new[] { "ComponentIndustrial", "Steel", "WoodLog" }.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n))
                .FirstOrDefault(d => d != null && (d.defName != "ComponentIndustrial" || Avail.EffectiveTech >= TechLevel.Industrial));

        /// <summary>What it gives for a term kept, by its domain: a healer mech serum for one of healing, else advanced components.</summary>
        private static ThingDef Reward(God g) =>
            (g.domain == GodDomain.Healing || g.second == GodDomain.Healing ? DefDatabase<ThingDef>.GetNamedSilentFail("MechSerumHealer") : null)
            ?? DefDatabase<ThingDef>.GetNamedSilentFail("ComponentSpacer");

        internal static void Terms(GodWorldBook b)
        {
            if (!GodWorld.CanAsk || b.Of("term").Any() || !b.Gap("term", GodWorldMath.BargainGap) || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.BargainDays, GodWorld.Factor()))) return;
            var g = GodWorld.Known.Where(x => x.kind == GodKind.Archotech).RandomElementWithFallback();
            var home = Homes.Seat;
            var goods = Goods();
            var reward = g != null ? Reward(g) : null;
            if (g == null || home == null || goods == null || reward == null) return;
            int count = GodWorldMath.TermCount(goods.BaseMarketValue, GodWorld.Wealth(home));
            int rcount = Mathf.Max(1, Mathf.RoundToInt(count * goods.BaseMarketValue * GodWorldMath.TermReward / Mathf.Max(1f, reward.BaseMarketValue)));
            string what = goods.defName + "|" + count + "|" + reward.defName + "|" + rcount;
            string text = GodWorld.T("CC_GW_Term_Text", g, count.Named("COUNT"), goods.label.Named("GOODS"), GodWorldMath.TermDays.Named("DAYS"), rcount.Named("RCOUNT"),
                reward.label.Named("REWARD"));
            if (GodAsks.Ask(GodAsks.Term, home, g, GodWorld.T("CC_GW_Term_Label", g), text, null, null, null, null, count, what, -1, 2f) != null) b.Came("term");
        }

        private static (ThingDef goods, int count, ThingDef reward, int rcount) ParseTerm(string what)
        {
            var p = (what ?? "").Split('|');
            if (p.Length < 4) return (null, 0, null, 0);
            int.TryParse(p[1], out int c);
            int.TryParse(p[3], out int r);
            return (DefDatabase<ThingDef>.GetNamedSilentFail(p[0]), c, DefDatabase<ThingDef>.GetNamedSilentFail(p[2]), r);
        }

        public static IEnumerable<GodAnswer> TermAnswers(ChoiceLetter_GodWorld l)
        {
            if (l.God == null || GodWorld.Book == null || GodWorld.Book.Of("term").Any()) yield break;
            yield return new GodAnswer("CC_GW_Term_Accept".Translate().Resolve(), x =>
            {
                var b = GodWorld.Book;
                b?.Add(new GodDoing { kind = "term", god = x.god, home = x.home, what = x.what, until = Now + GodWorldMath.TermDays * GodWorldMath.DayTicks });
            });
            yield return new GodAnswer("CC_GW_Term_Decline".Translate().Resolve(), x => { });
        }

        /// <summary>A term under way: delivered as soon as the stores hold it (carried out to the gate), the reward from orbit the next day; missed, broken.</summary>
        private static void TermsDue(GodWorldBook b, int now)
        {
            foreach (var d in b.Of("term").ToList())
            {
                var g = Gods.ById(d.god);
                var map = d.Map;
                var (goods, count, reward, rcount) = ParseTerm(d.what);
                if (g == null || map == null || goods == null || reward == null)
                {
                    d.done = true;
                    continue;
                }
                if (d.stage == 1)
                {
                    // Kept: exactly what it said, from orbit, at the colony's middle.
                    d.done = true;
                    var things = new List<Thing>();
                    int left = rcount;
                    while (left > 0)
                    {
                        var t = ThingMaker.MakeThing(reward);
                        t.stackCount = Mathf.Min(left, reward.stackLimit);
                        left -= t.stackCount;
                        things.Add(t);
                    }
                    var at = CampSite.ColonyCenter(map);
                    if (!at.IsValid) at = map.Center;
                    DropPodUtility.DropThingsNear(at, map, things, 110, false, false, false);
                    Gods.AddFavour(g, map, GodWorldMath.TermKept, GodWorld.T("CC_GW_Term_WhyKept", g));
                    GodWorld.Moment(g, map, PresenceKind.Act, at, null, GodWorld.T("CC_GW_Term_KeptLabel", g),
                        GodWorld.Voice(GodWorld.T("CC_GW_Term_Kept", g, rcount.Named("RCOUNT"), reward.label.Named("REWARD"))), true, LetterDefOf.PositiveEvent,
                        new LookTargets(new TargetInfo(at, map)));
                    continue;
                }
                if (now > d.until)
                {
                    d.done = true;
                    Gods.AddFavour(g, map, GodWorldMath.TermBroken, GodWorld.T("CC_GW_Term_WhyBroken", g), FavourSource.Deed, GodOffence.BrokenWord);
                    GodWorld.Moment(g, map, PresenceKind.Act, IntVec3.Invalid, null, GodWorld.T("CC_GW_Term_BrokenLabel", g),
                        GodWorld.Voice(GodWorld.T("CC_GW_Term_Broken", g, count.Named("COUNT"), goods.label.Named("GOODS"))), true, LetterDefOf.NegativeEvent);
                    continue;
                }
                if (PlayCarry.Have(map, goods.defName, goods) < count) continue;
                // apart: the colony meets its term: one of it carries the goods out past the gate, where they are taken in the night.
                new DilemmaEffect_CarryOut { what = goods.defName, count = count, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = GodWorld.T("CC_GW_Term_CarryLabel", g) }
                    .Apply(new DilemmaContext { map = map, thing = goods });
                d.stage = 1;
                ColonyCouncil.Tell.Minor(NewsKind.World, Importance.Notable, map, GodWorld.T("CC_GW_Term_Delivered", g, count.Named("COUNT"), goods.label.Named("GOODS")), null);
            }
        }

        // ================= The dead god's mark =================

        internal static void DeadMark(GodWorldBook b)
        {
            var dead = Gods.All.FirstOrDefault(g => g.kind == GodKind.Dead);
            if (dead == null || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.DeadMarkDays, GodWorld.Factor()))) return;
            foreach (var map in Homes.All.ToList())
            {
                string key = "deadmark:" + Homes.KeyOf(map);
                if (b.Told(key)) continue;
                var cell = GodCamps.FindSite(map, "stone", out var thing);
                if (!cell.IsValid) cell = GodCamps.FindSite(map, "wreck", out thing);
                if (!cell.IsValid || thing == null) continue;
                var finder = GodWorld.Grown(map).Where(p => p.Position.InHorDistOf(cell, 12f)).FirstOrDefault();
                if (finder == null) continue;
                b.Tell(key);
                Gods.Learn(dead, "sign");
                Gods.AddFavour(dead, map, GodWorldMath.DeadMarkFavour, GodWorld.T("CC_GW_DeadMark_Why", dead, finder.Named("PAWN")));
                FleckMaker.ThrowDustPuff(cell, map, 1.2f);
                GodWorld.Moment(dead, map, PresenceKind.Omen, cell, null, GodWorld.T("CC_GW_DeadMark_Label", dead),
                    GodWorld.T("CC_GW_DeadMark_Text", dead, finder.Named("PAWN"), thing.LabelNoCount.Named("THING")), true, LetterDefOf.NeutralEvent, new LookTargets(thing));
                GodWorld.Say(finder, "deadmark", dead);
                // Its few faithful hope again.
                foreach (var f in dead.factions.Where(Arrivals.Usable)) StoryChains.Goodwill(f, 2);
                return;
            }
        }

        // ================= The fading god =================

        internal static void Fading(GodWorldBook b)
        {
            if (!GodWorld.CanAsk || !b.Gap("fading", 120f) || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.FadingDays, GodWorld.Factor(true)))) return;
            var g = Gods.All.Where(x => x.kind == GodKind.Fading).RandomElementWithFallback();
            var home = Homes.Seat;
            if (g == null || home == null) return;
            if (!Gods.Known(g)) Gods.Learn(g, "sign");
            bool gentle = GodWorldMath.FadingGentle(g.nature);
            string text = GodWorld.Voice(GodWorld.T(gentle ? "CC_GW_Fading_Gentle" : "CC_GW_Fading_Bitter", g));
            if (GodAsks.Ask(GodAsks.Fading, home, g, GodWorld.T("CC_GW_Fading_Label", g), text, null, null, null, null, gentle ? 1 : 0, null, -1, 2f) != null) b.Came("fading");
        }

        public static IEnumerable<GodAnswer> FadingAnswers(ChoiceLetter_GodWorld l)
        {
            var g = l.God;
            if (g == null || g.kind != GodKind.Fading) yield break;
            yield return new GodAnswer("CC_GW_Fading_Remember".Translate(g.name.Named("GOD")).Resolve(), Remembered);
            yield return new GodAnswer((l.count == 1 ? "CC_GW_Fading_LetGo" : "CC_GW_Fading_Refuse").Translate().Resolve(), Forgotten);
        }

        private static void Remembered(ChoiceLetter_GodWorld l)
        {
            var g = l.God;
            var map = l.HomeMap;
            if (g == null || map == null) return;
            Gods.AddFavour(g, map, GodWorldMath.FadingRemembered, GodWorld.T("CC_GW_Fading_WhyRemembered", g), FavourSource.Rite);
            // Whoever says its name tonight is the better for it: a gentle god's thanks, an inspiration for one of the colony.
            var p = GodWorld.Grown(map).Where(x => x.mindState?.inspirationHandler != null && !x.mindState.inspirationHandler.Inspired).RandomElementWithFallback();
            var insp = DefDatabase<InspirationDef>.AllDefsListForReading.Where(d => p != null && d.Worker.InspirationCanOccur(p)).RandomElementWithFallback();
            if (p != null && insp != null && l.count == 1) p.mindState.inspirationHandler.TryStartInspiration(insp, GodWorld.T("CC_GW_Fading_Inspired", g));
            GodWorld.Remark(map, IntVec3.Invalid, "fading", g);
        }

        private static void Forgotten(ChoiceLetter_GodWorld l)
        {
            var g = l.God;
            var map = l.HomeMap;
            if (g == null || map == null) return;
            if (l.count == 1) return;
            // A bitter one's spite: the sun put out an afternoon (the game's own eclipse).
            Gods.AddFavour(g, map, GodWorldMath.FadingForgotten, GodWorld.T("CC_GW_Fading_WhyForgotten", g));
            GodHealth.EclipseOf(g, map);
        }

        // ================= The Sleeper's door =================

        private static God SleeperGod => Gods.All.FirstOrDefault(g => g.kind == GodKind.Imprisoned || g.baseKind == GodKind.Imprisoned);

        private static Faction Vault(God g) => g?.factions.FirstOrDefault(f => f != null && !f.IsPlayer && !f.defeated && f.HasGoodwill);

        internal static void Sleeper(GodWorldBook b)
        {
            var g = SleeperGod;
            if (g == null || !Gods.Known(g) || b.sleeperStep >= GodWorldMath.SleeperSteps || !GodWorld.CanAsk || !b.Gap("sleeper", GodWorldMath.SleeperGap)
                || !GodWorld.Roll(GodWorldMath.DayChance(GodWorldMath.SleeperDays, GodWorld.Factor(true))))
                return;
            var home = Homes.Seat;
            var dreamer = GodWorld.Grown(home).OrderByDescending(p => p.GetStatValue(StatDefOf.PsychicSensitivity)).FirstOrDefault();
            if (home == null || dreamer == null) return;
            int step = b.sleeperStep;
            var goods = step == 0 ? Goods() : null;
            int count = goods != null ? GodWorldMath.TermCount(goods.BaseMarketValue, GodWorld.Wealth(home)) : 0;
            string text = GodWorld.Voice(GodWorld.T("CC_GW_Sleeper_Dream", g, dreamer.Named("PAWN"))) + "\n\n"
                          + GodWorld.T("CC_GW_Sleeper_Ask_" + step, g, count.Named("COUNT"), (goods?.label ?? "").Named("GOODS"), (Vault(g)?.Name ?? "?").Named("PEOPLE"));
            if (GodAsks.Ask(GodAsks.Sleeper, home, g, GodWorld.T("CC_GW_Sleeper_Label", g, dreamer.Named("PAWN")), text, new LookTargets(dreamer), null, Vault(g), dreamer, count,
                    goods?.defName, -1, 3f) != null)
                b.Came("sleeper");
        }

        public static IEnumerable<GodAnswer> SleeperAnswers(ChoiceLetter_GodWorld l)
        {
            var g = l.God;
            var b = GodWorld.Book;
            var map = l.HomeMap;
            if (g == null || b == null || map == null || b.sleeperStep >= GodWorldMath.SleeperSteps) yield break;
            var goods = l.what != null ? DefDatabase<ThingDef>.GetNamedSilentFail(l.what) : null;
            string disabled = goods != null && PlayCarry.Have(map, goods.defName, goods) < l.count ? "CC_Dilemma_NotEnough".Translate(l.count, goods.label).ToString() : null;
            yield return new GodAnswer("CC_GW_Sleeper_Do".Translate().Resolve(), SleeperStep, disabled);
            yield return new GodAnswer("CC_GW_Sleeper_Refuse".Translate().Resolve(), x =>
            {
                if (x.God != null) Gods.AddFavour(x.God, x.HomeMap, GodWorldMath.SleeperRefused, GodWorld.T("CC_GW_Sleeper_WhyRefused", x.God));
            });
        }

        private static void SleeperStep(ChoiceLetter_GodWorld l)
        {
            var g = l.God;
            var b = GodWorld.Book;
            var map = l.HomeMap;
            if (g == null || b == null || map == null) return;
            var goods = l.what != null ? DefDatabase<ThingDef>.GetNamedSilentFail(l.what) : null;
            if (goods != null && l.count > 0)
                // apart: what the Sleeper asked for is carried out past the gate and left in the dark for it.
                new DilemmaEffect_CarryOut { what = goods.defName, count = l.count, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = GodWorld.T("CC_GW_Sleeper_CarryLabel", g) }
                    .Apply(new DilemmaContext { map = map, thing = goods });
            b.sleeperStep++;
            Gods.AddFavour(g, map, GodWorldMath.SleeperStep, GodWorld.T("CC_GW_Sleeper_WhyStep", g));
            var vault = Vault(g);
            if (b.sleeperStep < GodWorldMath.SleeperSteps)
            {
                if (vault != null) StoryChains.Goodwill(vault, GodWorldMath.VaultGoodwill);
                GodWorld.Remember(g, map, GodActKind.Act, GodWorld.T("CC_GW_Sleeper_StepAct", g));
                return;
            }
            // The third: the door opens a crack.
            if (vault != null)
            {
                StoryChains.Goodwill(vault, GodWorldMath.VaultOpened);
                FactionMemory.Note(vault, "slighted", g.name);
            }
            try
            {
                var def = GameConditionDefOf.PsychicSoothe;
                if (def != null && !map.gameConditionManager.ConditionIsActive(def))
                {
                    var cond = GameConditionMaker.MakeCondition(def, 2 * GenDate.TicksPerDay);
                    if (cond is GameCondition_PsychicEmanation pe) pe.gender = Rand.Bool ? Gender.Male : Gender.Female;
                    map.gameConditionManager.RegisterCondition(cond);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: the Sleeper's calm", e);
            }
            GodWorld.Moment(g, map, PresenceKind.Reveal, IntVec3.Invalid, null, GodWorld.T("CC_GW_Sleeper_OpenLabel", g),
                GodWorld.Voice(GodWorld.T("CC_GW_Sleeper_Open", g, (vault?.Name ?? "?").Named("PEOPLE"))), true, LetterDefOf.NeutralEvent, null, vault, Importance.Important);
            GodWorld.Chronicle(GodWorld.T("CC_GW_Sleeper_Chronicle", g));
        }

        // ================= A newborn's first signs =================

        internal static void Newborn(GodWorldBook b)
        {
            foreach (var g in Gods.All.Where(x => x.kind == GodKind.Newborn).ToList())
            {
                int told = 0;
                for (int s = 1; s <= 3; s++)
                    if (b.Told("newborn:" + g.id + ":" + s))
                        told = s;
                int sign = GodWorldMath.NewbornSign(g.devotion, GodsMath.NewbornGrown, told);
                if (sign < 1) continue;
                b.Tell("newborn:" + g.id + ":" + sign);
                var map = Homes.Seat;
                if (map == null) continue;
                var centre = CampSite.ColonyCenter(map);
                if (sign == 3)
                {
                    // It answers a small prayer: something small found where nobody had looked.
                    var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                    silver.stackCount = Rand.RangeInclusive(20, 40);
                    if (centre.IsValid) GenPlace.TryPlaceThing(silver, centre, map, ThingPlaceMode.Near);
                }
                if (centre.IsValid) FleckMaker.ThrowMetaIcon(centre, map, FleckDefOf.Heart);
                GodWorld.Moment(g, map, PresenceKind.Omen, centre, null, GodWorld.T("CC_GW_Newborn_Label", g),
                    GodWorld.Voice(GodWorld.T("CC_GW_Newborn_Sign_" + sign, g)), sign == 3, LetterDefOf.PositiveEvent);
                GodWorld.Remark(map, centre, "newborn", g);
                return;
            }
        }
    }
}
