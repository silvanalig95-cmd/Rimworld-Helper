using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The end of a long life (the numbers are Ailments/LifeRiskMath.cs). Past 78 (in human years, so long-lived genes
    /// count), a colonist may begin to fail: a tenth a year at 80, more than half at 90. A letter says so when it begins,
    /// and they write of it. Over about a season they slow down, sleep more and eat less (Council_LastSeason's stages),
    /// slower while they're content, have a room of their own and have had company of their own, faster when they're
    /// miserable; made young enough again (an age reversal), it turns back. Near the end the colony is told, and those
    /// closest to them go to sit with them while there's time (PlayOut/: visits). At the end they die in their sleep,
    /// the game's own death, with all that follows it. The "endOfLife" setting turned off clears it from everyone.
    /// Saved only on the hediff itself.
    /// </summary>
    public static class LastSeason
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => S != null && S.endOfLife;

        private static HediffDef def;
        public static HediffDef Def => def ??= DefDatabase<HediffDef>.GetNamedSilentFail("Council_LastSeason");

        private static GeneDef deathless;
        private static bool lookedDeathless;

        private static ThoughtDef T(string name) => DefDatabase<ThoughtDef>.GetNamedSilentFail(name);

        /// <summary>One who can come to the end of a long life: one of ours, a person, not one who can't die.</summary>
        private static bool Mortal(Pawn p)
        {
            if (p == null || p.Dead || p.RaceProps == null || !p.RaceProps.Humanlike || !p.IsColonist || p.IsQuestLodger()) return false;
            if (!lookedDeathless)
            {
                lookedDeathless = true;
                deathless = DefDatabase<GeneDef>.GetNamedSilentFail("Deathless");
            }
            return deathless == null || p.genes == null || !p.genes.HasActiveGene(deathless);
        }

        /// <summary>Once a day: someone old enough may begin to fail.</summary>
        public static void Daily(CouncilGameComponent comp)
        {
            if (!On || Def == null) return;
            foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.ToList())
            {
                // Up and about at home or on the road: never asleep in a cryptosleep casket.
                if (!Mortal(p) || (!p.Spawned && p.GetCaravan() == null) || p.health.hediffSet.HasHediff(Def)) continue;
                float chance = LifeRiskMath.DailyChance(LifeRiskMath.YearlyChance(Flavor.HumanAge(p)));
                if (chance > 0f && Rand.Chance(chance)) Begin(comp, p);
            }
        }

        /// <summary>The decline begins: the hediff, the letter (with a last wish they still have), and a line in their journal.</summary>
        public static void Begin(CouncilGameComponent comp, Pawn p)
        {
            // apart: a letter about one person's health; what it says is on their Health tab from now on.
            if (Def == null || p == null) return;
            p.health.AddHediff(Def);
            int age = (int)p.ageTracker.AgeBiologicalYearsFloat;
            string text = "CC_Life_LastSeason_Text".Translate(p.Named("PAWN"), age.Named("AGE")).Resolve();
            var wish = comp?.aspirations?.Active(p);
            if (wish != null && wish.def.lastWish) text += "\n\n" + "CC_Life_LastSeason_Wish".Translate(p.Named("PAWN"), wish.Goal.Named("GOAL")).Resolve();
            Find.LetterStack.ReceiveLetter("CC_Life_LastSeason_Label".Translate(p.Named("PAWN")).Resolve(), text, LetterDefOf.NegativeEvent, new LookTargets(p));
            if (comp != null && !comp.journals.WroteWithin(p)) comp.journals.Write(p, "Council_Journal_LastSeason", null, age.Named("YEARS"));
        }

        /// <summary>Company of their own lately: a good memory of the last day about family, a partner or a close friend.</summary>
        private static bool Company(Pawn p)
        {
            var memories = p.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null || p.relations == null) return false;
            foreach (var m in memories)
            {
                var o = m.otherPawn;
                if (o == null || o == p || o.Dead || m.age > GenDate.TicksPerDay) continue;
                if (m.MoodOffset() <= 0f) continue;
                if (Family.Of(p, o) || LovePartnerRelationUtility.LovePartnerRelationExists(p, o) || p.relations.OpinionOf(o) >= 40) return true;
            }
            return false;
        }

        private static float Mood(Pawn p) => p.needs?.mood?.CurLevelPercentage ?? 0.5f;

        private static bool OwnRoom(Pawn p) => p.ownership?.Bedroom != null;

        /// <summary>Each hour: on it goes, or back if they were made young again; near the end the family comes; at the end, in their sleep.</summary>
        public static void Hour(HediffComp_LastSeason c)
        {
            var h = c.parent;
            var p = h?.pawn;
            if (p == null || p.Dead) return;
            if (!On)
            {
                c.remove = true;
                return;
            }
            float age = Flavor.HumanAge(p);
            if (LifeRiskMath.TurnsBack(age))
            {
                h.Severity -= LifeRiskMath.RecoverPerDay / 24f;
                if (h.Severity <= 0.002f)
                {
                    c.remove = true;
                    Messages.Message("CC_Life_LastSeason_Turned".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.PositiveEvent, false);
                }
                return;
            }
            h.Severity = Math.Min(1f, h.Severity + LifeRiskMath.DeclinePerDay(Mood(p), OwnRoom(p), Company(p)) / 24f);
            if (h.Severity >= LifeRiskMath.NearTheEnd && !c.toldNear)
            {
                c.toldNear = true;
                try
                {
                    Near(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the end of a long life: near", e);
                }
            }
            if (h.Severity < 1f) return;
            int now = Find.TickManager.TicksGame;
            if (c.endTick < 0) c.endTick = now;
            // In their sleep: in bed, or asleep anywhere once a couple of days have gone by at the end.
            if (p.Awake() || (!p.InBed() && now - c.endTick < GenDate.TicksPerDay * 2)) return;
            Die(p, h);
        }

        /// <summary>The end is near: the colony is told, and those closest go to sit with them while there's time.</summary>
        private static void Near(Pawn p)
        {
            // apart: the news is told to the colony; each goodbye is a visit on the map (PlayOut/).
            var visitors = new List<Pawn>();
            if (p.Spawned && p.Map.IsPlayerHome && PlayOuts.On)
                visitors = p.Map.mapPawns.FreeColonistsSpawned
                    .Where(o => o != p && !o.IsQuestLodger() && People.HasInnerLife(o) && !o.DevelopmentalStage.Baby() && !o.Downed
                                && (Family.Of(o, p) || LovePartnerRelationUtility.LovePartnerRelationExists(o, p) || o.relations.OpinionOf(p) >= 40))
                    .OrderByDescending(o => LovePartnerRelationUtility.LovePartnerRelationExists(o, p)).ThenByDescending(o => Family.Of(o, p))
                    .ThenByDescending(o => o.relations.OpinionOf(p)).Take(3).ToList();
            var going = new List<Pawn>();
            foreach (var o in visitors)
            {
                var play = new DilemmaEffect_Visit
                {
                    who = PlayWho.A, to = PlaySpot.Person, whom = PlayWho.B, sit = true, label = "CC_Life_LastVisitLabel".Translate(), hours = new IntRange(8, 22),
                    duration = new FloatRange(1f, 1.5f), waitHours = 12f
                }.Begin(new DilemmaContext { map = p.Map, a = o, b = p });
                if (play == null) continue;
                going.Add(o);
                string cause = "CC_Life_LastVisitCause".Translate(o.Named("A"), p.Named("B")).Resolve();
                play.moods.Add(new HeldMood { pawn = o, thought = T("Council_Life_SatWithThem"), about = p, cause = cause });
                play.moods.Add(new HeldMood { pawn = p, thought = T("Council_Life_NotAlone"), about = o, cause = cause });
                play.moods.RemoveAll(m => m.thought == null);
            }
            string text = going.Count > 0
                ? "CC_Life_LastSeason_Near".Translate(p.Named("PAWN"), TextUtil.JoinAnd(going.Select(x => x.LabelShort).ToList()).Named("WHO")).Resolve()
                : "CC_Life_LastSeason_NearAlone".Translate(p.Named("PAWN")).Resolve();
            Messages.Message(text, new LookTargets(p), MessageTypeDefOf.NegativeEvent, false);
        }

        private static void Die(Pawn p, Hediff h)
        {
            // apart: the game's own death, in their sleep; grief, the grave and what they leave follow from it.
            var comp = CouncilGameComponent.Instance;
            int age = (int)p.ageTracker.AgeBiologicalYearsFloat;
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Life_LastSeason_Chronicle".Translate(p.Named("PAWN"), age.Named("AGE")).Resolve(), p));
            p.Kill(null, h);
        }

        /// <summary>For the hediff's tooltip: what slows it now, or hastens it, and how the end comes.</summary>
        public static string Tip(HediffComp_LastSeason c)
        {
            var p = c?.parent?.pawn;
            if (p == null || p.Dead) return null;
            if (LifeRiskMath.TurnsBack(Flavor.HumanAge(p))) return "CC_Life_LastSeason_TipTurning".Translate().Resolve();
            var slower = new List<string>();
            if (Mood(p) >= 0.7f) slower.Add("CC_Life_LastSeason_Content".Translate().Resolve());
            if (OwnRoom(p)) slower.Add("CC_Life_LastSeason_Room".Translate().Resolve());
            if (Company(p)) slower.Add("CC_Life_LastSeason_Company".Translate().Resolve());
            var lines = new List<string>();
            if (slower.Count > 0) lines.Add("CC_Life_LastSeason_Slower".Translate(string.Join(", ", slower).Named("WHAT")).Resolve());
            if (Mood(p) <= 0.3f) lines.Add("CC_Life_LastSeason_Faster".Translate().Resolve());
            if (slower.Count == 0 && Mood(p) > 0.3f) lines.Add("CC_Life_LastSeason_Helps".Translate().Resolve());
            lines.Add("CC_Life_LastSeason_Sleep".Translate().Resolve());
            return string.Join("\n", lines);
        }
    }

    public class HediffCompProperties_LastSeason : HediffCompProperties
    {
        public HediffCompProperties_LastSeason()
        {
            compClass = typeof(HediffComp_LastSeason);
        }
    }

    /// <summary>The end of a long life, on the one it's coming to (<see cref="LastSeason"/>): once an hour.</summary>
    public class HediffComp_LastSeason : HediffComp
    {
        /// <summary>The colony has been told the end is near.</summary>
        public bool toldNear;
        /// <summary>When it reached the end (they die in their sleep after it), or -1.</summary>
        public int endTick = -1;
        /// <summary>Over (the setting turned off, or made young again): the game takes it off at its next look.</summary>
        public bool remove;

        public override bool CompShouldRemove => remove;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            var p = Pawn;
            if (p == null || p.Dead || !p.IsHashIntervalTick(GenDate.TicksPerHour, delta)) return;
            try
            {
                LastSeason.Hour(this);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the end of a long life", e);
            }
        }

        public override string CompTipStringExtra => LastSeason.Tip(this);

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref toldNear, "toldNear");
            Scribe_Values.Look(ref endTick, "endTick", -1);
        }
    }
}
