using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The void as a dark god (docs/design/gods.md, stage 2, brainstorm item 22): the Hunger, the Choir of the Spire's (or
    /// the world's darkest void god), answers anyone desperate enough, always pays, and always takes more.
    /// <list type="bullet">
    /// <item>It hears desperation (GodWorldMath.Desperation): one of the colony dying (bleeding out within hours, or an
    /// illness about to win), at the edge of a breakdown, or the colony out of food. Its question comes when it comes, not
    /// when questions may: a voice offers, and asks nothing yet.</item>
    /// <item>Taken, it pays at once and plainly: the bleeding stops and the illness falls back, the mind goes still (a calm
    /// out of the dark for two days), or meat out of the dark lies at the colony's door (Anomaly's twisted meat, else
    /// pemmican).</item>
    /// <item>Then it comes to take, sooner each time (GodWorldMath.HungerTake): a mark of the void on the one who bargained
    /// (Anomaly's void-touched, else its mark), years of their life, their humanity (Anomaly's inhumanized, else their mind
    /// for a while), a beast of the colony's into the dark. Its next whisper comes sooner too.</item>
    /// <item>The Choir of the Spire know the voice: they write when the colony has bargained, and think better of an
    /// honest refusal.</item>
    /// </list>
    /// Only where the gods are more than believed.
    /// </summary>
    public static class GodVoid
    {
        public static God Hunger => Gods.All.FirstOrDefault(g => g.baseKind == GodKind.Hunger || g.kind == GodKind.Hunger);

        /// <summary>The Choir of the Spire, the Hunger's people, where they are in the world.</summary>
        public static Faction Choir(God hunger) => hunger?.factions.FirstOrDefault(f => f != null && !f.IsPlayer && !f.defeated && f.def?.defName == "Rimfolk_Choir")
                                                   ?? hunger?.factions.FirstOrDefault(f => f != null && !f.IsPlayer && !f.defeated);

        // ================= Who is desperate =================

        private static bool Dying(Pawn p)
        {
            if (p?.health?.hediffSet == null || p.Dead) return false;
            if (p.Downed && p.health.hediffSet.BleedRateTotal > 0.01f && HealthUtility.TicksUntilDeathDueToBloodLoss(p) < 6 * GenDate.TicksPerHour) return true;
            return p.health.hediffSet.hediffs.Any(h => h.def.lethalSeverity > 0f && h.Severity / h.def.lethalSeverity > 0.82f && h is HediffWithComps hw
                                                       && hw.TryGetComp<HediffComp_Immunizable>() is HediffComp_Immunizable imm && imm.Immunity < h.Severity);
        }

        private static bool Breaking(Pawn p) => p?.mindState?.mentalBreaker != null && !p.Downed && !p.InMentalState && p.mindState.mentalBreaker.BreakExtremeIsImminent;

        private static bool Starving(Map map)
        {
            int people = map.mapPawns.FreeColonistsSpawnedCount;
            return people > 0 && map.resourceCounter.TotalHumanEdibleNutrition < 0.3f * people
                   && map.mapPawns.FreeColonistsSpawned.Any(p => p.needs?.food != null && p.needs.food.CurCategory >= HungerCategory.UrgentlyHungry);
        }

        // ================= The hour: it hears =================

        public static void Hourly(GodWorldBook b, int now)
        {
            var g = Hunger;
            if (g == null || !GodWorldMath.Supernatural(Gods.Realness) || !b.Gap("hunger", GodWorldMath.HungerGapDays(b.hungerBargains))) return;
            foreach (var map in Homes.All.ToList())
            {
                if (map == null || !b.Gap("hunger:" + Homes.KeyOf(map), 1f)) continue;
                var people = map.mapPawns.FreeColonistsSpawned.ToList();
                var dying = people.FirstOrDefault(Dying);
                var breaking = dying == null ? people.FirstOrDefault(Breaking) : null;
                bool starving = dying == null && breaking == null && Starving(map);
                int desperation = GodWorldMath.Desperation(dying != null, breaking != null, starving);
                if (desperation <= 0) continue;
                // Looked at once a day at a home, whatever it hears.
                b.Came("hunger:" + Homes.KeyOf(map));
                if (!GodWorld.Roll(GodWorldMath.HungerHears(desperation) * GodWorld.Factor(true))) continue;
                Whisper(b, g, map, dying ?? breaking, dying != null ? "dying" : breaking != null ? "breaking" : "starving");
                return;
            }
        }

        private static void Whisper(GodWorldBook b, God g, Map map, Pawn p, string why)
        {
            var hearer = p ?? map.mapPawns.FreeColonistsSpawned.Where(x => x.needs?.mood != null).OrderBy(x => x.needs.mood.CurLevel).FirstOrDefault();
            if (hearer == null) return;
            if (!Gods.Known(g)) Gods.Learn(g, "void");
            b.Came("hunger");
            string label = GodWorld.T("CC_GW_Hunger_Label", g, hearer.Named("PAWN"));
            string text = GodWorld.Voice(GodWorld.T("CC_GW_Hunger_" + why, g, hearer.Named("PAWN")));
            if (b.hungerTaken > 0) text += "\n\n" + GodWorld.T("CC_GW_Hunger_Again", g);
            GodAsks.Ask(GodAsks.Hunger, map, g, label, text, new LookTargets(hearer), null, Choir(g), hearer, 0, why, -1, 0.25f);
            GodWorld.Remember(g, map, GodActKind.Omen, GodWorld.T("CC_GW_Hunger_Act", g, hearer.Named("PAWN")));
        }

        public static IEnumerable<GodAnswer> BargainAnswers(ChoiceLetter_GodWorld l)
        {
            var p = l.pawn;
            var map = l.HomeMap;
            if (l.God == null || map == null || p == null || p.Dead) yield break;
            // What it offered must still be needed: the dying still dying, the breaking still at the edge, the stores still bare.
            bool still = l.what == "dying" ? Dying(p) : l.what == "breaking" ? Breaking(p) || p.InMentalState : Starving(map);
            if (!still) yield break;
            yield return new GodAnswer("CC_GW_Hunger_Take".Translate().Resolve(), Take);
            yield return new GodAnswer("CC_GW_Hunger_Refuse".Translate().Resolve(), Refuse);
        }

        // ================= It pays =================

        private static void Take(ChoiceLetter_GodWorld l)
        {
            var g = l.God;
            var p = l.pawn;
            var map = l.HomeMap;
            var b = GodWorld.Book;
            if (g == null || p == null || map == null || b == null) return;
            string paid;
            try
            {
                paid = l.what == "dying" ? Mend(p) : l.what == "breaking" ? Still(p) : Feed(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: the Hunger pays", e);
                return;
            }
            if (paid == null) return;
            b.hungerBargains++;
            b.Add(new GodDoing
            {
                kind = "take", god = g.id, pawn = p, home = Homes.KeyOf(map), stage = b.hungerTaken,
                at = GodWorld.Now + GodWorldMath.HungerTakeAfterDays(b.hungerTaken) * GodWorldMath.DayTicks, until = GodWorld.Now + 30 * GodWorldMath.DayTicks
            });
            Gods.AddFavour(g, map, GodWorldMath.HungerStruck, GodWorld.T("CC_GW_Hunger_WhyStruck", g, p.Named("PAWN")), FavourSource.Offering);
            if (p.Spawned) FleckMaker.ThrowMetaIcon(p.Position, map, FleckDefOf.PsycastAreaEffect);
            GodWorld.Moment(g, map, PresenceKind.Act, p.Spawned ? p.Position : IntVec3.Invalid, null, GodWorld.T("CC_GW_Hunger_PaidLabel", g),
                GodWorld.Voice(GodWorld.T("CC_GW_Hunger_Paid", g, p.Named("PAWN"), paid.Named("PAID"))), true, LetterDefOf.NeutralEvent, p.Spawned ? new LookTargets(p) : null);
            GodWorld.Chronicle(GodWorld.T("CC_GW_Hunger_Chronicle", g, p.Named("PAWN")), p);
            var choir = Choir(g);
            if (choir != null && Arrivals.Usable(choir))
            {
                StoryChains.Goodwill(choir, GodWorldMath.ChoirGoodwill);
                ColonyCouncil.Tell.Letter(NewsKind.World, Importance.Notable, map, "CC_GW_Choir_Label".Translate(choir.Name.Named("PEOPLE")).Resolve(),
                    GodWorld.T("CC_GW_Choir_Text", g, choir.Name.Named("PEOPLE"), p.Named("PAWN")), LetterDefOf.NeutralEvent, null, choir);
            }
        }

        private static void Refuse(ChoiceLetter_GodWorld l)
        {
            var g = l.God;
            var map = l.HomeMap;
            if (g == null) return;
            Gods.AddFavour(g, map, GodWorldMath.HungerRefused, GodWorld.T("CC_GW_Hunger_WhyRefused", g, (l.pawn?.LabelShort ?? "?").Named("PAWN")));
            var choir = Choir(g);
            if (choir != null && Arrivals.Usable(choir)) StoryChains.Goodwill(choir, 1);
        }

        /// <summary>The dying mended: every bleeding wound closed, the blood they lost partly back, an illness about to win set back.</summary>
        private static string Mend(Pawn p)
        {
            foreach (var h in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => h.Bleeding).ToList()) h.Tended(1f, 1f);
            var loss = p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (loss != null) loss.Severity = Mathf.Max(0.05f, loss.Severity - 0.3f);
            foreach (var h in p.health.hediffSet.hediffs.Where(h => h.def.lethalSeverity > 0f && h.Severity / h.def.lethalSeverity > 0.6f && h is HediffWithComps).ToList())
                h.Severity = Mathf.Max(0.05f, h.Severity - 0.3f * h.def.lethalSeverity);
            return GodWorld.T("CC_GW_Hunger_Mended", null, p.Named("PAWN"));
        }

        /// <summary>The breaking mind gone still: a calm out of the dark for two days (Rimfolk_VoidCalm).</summary>
        private static string Still(Pawn p)
        {
            var calm = DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_VoidCalm");
            if (calm == null) return null;
            if (!p.health.hediffSet.HasHediff(calm)) p.health.AddHediff(calm);
            return GodWorld.T("CC_GW_Hunger_Stilled", null, p.Named("PAWN"));
        }

        /// <summary>Meat out of the dark at the colony's door: a day's food for every colonist (Anomaly's twisted meat, else pemmican).</summary>
        private static string Feed(Map map)
        {
            var def = new[] { "Meat_Twisted", "Pemmican" }.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d?.ingestible != null);
            if (def == null) return null;
            int people = Mathf.Max(1, map.mapPawns.FreeColonistsSpawnedCount);
            float each = Mathf.Max(0.01f, def.GetStatValueAbstract(StatDefOf.Nutrition));
            int count = Mathf.CeilToInt(people * 1.6f / each);
            var at = CampSite.ColonyCenter(map);
            if (!at.IsValid) at = map.Center;
            int left = count;
            while (left > 0)
            {
                var t = ThingMaker.MakeThing(def);
                t.stackCount = Mathf.Min(left, def.stackLimit);
                left -= t.stackCount;
                GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
            }
            return "CC_GW_Hunger_Fed".Translate(count.Named("COUNT"), def.label.Named("FOOD")).Resolve();
        }

        // ================= It takes =================

        public static void Daily(GodWorldBook b, int now)
        {
            foreach (var d in b.Of("take").ToList())
            {
                if (now < d.at) continue;
                d.done = true;
                GodWorld.Safe("the Hunger takes", () => TakeDue(b, d));
            }
        }

        private static void TakeDue(GodWorldBook b, GodDoing d)
        {
            var g = Gods.ById(d.god);
            var map = d.Map;
            if (g == null || map == null) return;
            // The one who bargained, else whoever of the colony it finds.
            var p = d.pawn != null && !d.pawn.Dead && d.pawn.Spawned && d.pawn.Map == map ? d.pawn : map.mapPawns.FreeColonistsSpawned.Where(x => x.DevelopmentalStage.Adult()).RandomElementWithFallback();
            if (p == null) return;
            string what = GodWorldMath.HungerTake(b.hungerTaken);
            string took = null;
            try
            {
                switch (what)
                {
                    case "mark":
                        var mark = (ModsConfig.AnomalyActive ? HediffDefOf.VoidTouched : null) ?? DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_GodMark");
                        if (mark != null && !p.health.hediffSet.HasHediff(mark)) p.health.AddHediff(mark);
                        took = GodWorld.T("CC_GW_Hunger_TookMark", g, p.Named("PAWN"));
                        break;
                    case "years":
                        p.ageTracker.AgeBiologicalTicks += (long)GodWorldMath.HungerYears * GenDate.TicksPerYear;
                        took = "CC_GW_Hunger_TookYears".Translate(p.Named("PAWN"), GodWorldMath.HungerYears.Named("YEARS")).Resolve();
                        break;
                    case "mind":
                        if (ModsConfig.AnomalyActive && HediffDefOf.Inhumanized != null && !p.health.hediffSet.HasHediff(HediffDefOf.Inhumanized))
                        {
                            p.health.AddHediff(HediffDefOf.Inhumanized);
                            took = "CC_GW_Hunger_TookHumanity".Translate(p.Named("PAWN")).Resolve();
                        }
                        else if (p.mindState?.mentalStateHandler != null && p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Wander_Psychotic, null, true))
                            took = "CC_GW_Hunger_TookMind".Translate(p.Named("PAWN")).Resolve();
                        break;
                    default:
                        var beast = map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && a.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond) == null).RandomElementWithFallback();
                        if (beast != null)
                        {
                            FleckMaker.ThrowSmoke(beast.DrawPos, map, 1.5f);
                            took = "CC_GW_Hunger_TookBeast".Translate(beast.Named("BEAST")).Resolve();
                            beast.Destroy();
                        }
                        else
                        {
                            p.ageTracker.AgeBiologicalTicks += (long)GodWorldMath.HungerYears * GenDate.TicksPerYear;
                            took = "CC_GW_Hunger_TookYears".Translate(p.Named("PAWN"), GodWorldMath.HungerYears.Named("YEARS")).Resolve();
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods in the world: what the Hunger takes", e);
            }
            if (took == null) return;
            b.hungerTaken++;
            if (p.Spawned) FleckMaker.ThrowMetaIcon(p.Position, map, FleckDefOf.PsycastAreaEffect);
            GodWorld.Moment(g, map, PresenceKind.Wrath, p.Spawned ? p.Position : IntVec3.Invalid, null, GodWorld.T("CC_GW_Hunger_TookLabel", g, p.Named("PAWN")),
                GodWorld.Voice(GodWorld.T("CC_GW_Hunger_Took", g, p.Named("PAWN"), took.Named("TOOK"))), true, LetterDefOf.NegativeEvent, p.Spawned ? new LookTargets(p) : null);
            GodWorld.Remark(map, p.PositionHeld, "hunger", g, p);
        }
    }
}
