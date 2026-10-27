using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Realistic ways to die, each with a warning and a way to fight it (the numbers are LifeRiskMath.cs):
    /// <list type="bullet">
    /// <item>A crowded, dirty colony catches serious illness more often: people sleeping in barracks or with no bed, a
    /// dirty kitchen, and (gently) the colony's size divide the catching clock's days (AilmentTracker.Clock). The
    /// advisor says so once it counts, with the reasons.</item>
    /// <item>The frail fight an illness slower: old age, a small child, hunger and bad wounds lower the game's immunity
    /// gain speed (<see cref="StatPart_Frailty"/>, shown in the stat's breakdown), so steady good care is what saves
    /// them. A serious illness's letter says so for someone frail.</item>
    /// <item>A wound left untended in the dirt is likelier to turn bad: where the game's own infection roll for an
    /// untended wound fails, a further chance makes the whole as likely as the dirt where they lie says
    /// (<see cref="Patch_DirtyWound"/>), and a message says why.</item>
    /// </list>
    /// The end of a long life is Health/LastSeason.cs. Nothing here is saved.
    /// </summary>
    public static class LifeRisks
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool CrowdingOn => S != null && S.crowdedSickness && AilmentTracker.SeriousOn;
        public static bool FrailtyOn => S != null && S.frailty;
        public static bool DirtyWoundsOn => RimfolkPatches.On && S != null && S.dirtyWounds;

        // ---------------- Sickness in a crowded, dirty colony ----------------

        /// <summary>How much likelier a serious illness is here, and why.</summary>
        public class SicknessRisk
        {
            public float factor = 1f;
            public int people;
            public int crowded;
            public float? kitchen;
            public Room kitchenRoom;
        }

        private static readonly Dictionary<int, (int tick, SicknessRisk risk)> cache = new Dictionary<int, (int, SicknessRisk)>();

        public static void Reset() => cache.Clear();

        /// <summary>Sleeping crowded: no bed of their own, or one in barracks or out in the open.</summary>
        private static bool Crowded(Pawn p)
        {
            var bed = p.ownership?.OwnedBed;
            if (bed == null || !bed.Spawned) return true;
            var room = bed.GetRoom();
            return room == null || room.PsychologicallyOutdoors || room.Role == RoomRoleDefOf.Barracks;
        }

        /// <summary>The colony's sickness risk on this map (worked out at most once an hour).</summary>
        public static SicknessRisk Sickness(Map map)
        {
            if (map == null) return new SicknessRisk();
            int now = Find.TickManager.TicksGame;
            if (cache.TryGetValue(map.uniqueID, out var c) && now - c.tick < GenDate.TicksPerHour && now >= c.tick) return c.risk;
            var risk = new SicknessRisk();
            try
            {
                var people = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Baby()).ToList();
                risk.people = people.Count;
                risk.crowded = people.Count(Crowded);
                foreach (var room in RoomUtil.ColonyRooms(map))
                {
                    if (!RoomUtil.RoomContains(room, Avail.IsCookingStation)) continue;
                    float clean = room.GetStat(RoomStatDefOf.Cleanliness);
                    if (risk.kitchen.HasValue && clean >= risk.kitchen.Value) continue;
                    risk.kitchen = clean;
                    risk.kitchenRoom = room;
                }
                risk.factor = LifeRiskMath.SicknessFactor(risk.people, risk.crowded, risk.kitchen);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("sickness risk", e);
            }
            cache[map.uniqueID] = (now, risk);
            return risk;
        }

        /// <summary>The factor the catching clock divides serious illness's days by on this map (1 with the setting off).</summary>
        public static float SicknessFactor(Map map) => CrowdingOn ? Sickness(map).factor : 1f;

        // ---------------- The frail ----------------

        private static HediffDef malnutrition;

        /// <summary>How fast their body fights an illness, as a share of a healthy adult's, with what makes it slower.</summary>
        public static float Frailty(Pawn p, List<string> why = null)
        {
            if (!FrailtyOn || p?.RaceProps == null || !p.RaceProps.Humanlike || p.health == null) return 1f;
            float age = Flavor.HumanAge(p);
            malnutrition ??= HediffDefOf.Malnutrition;
            float hunger = malnutrition != null ? p.health.hediffSet?.GetFirstHediffOfDef(malnutrition)?.Severity ?? 0f : 0f;
            float health = p.health.summaryHealth?.SummaryHealthPercent ?? 1f;
            float f = LifeRiskMath.Frailty(age, hunger, health);
            if (why != null && f < 1f)
            {
                if (LifeRiskMath.AgeFrailty(age) < 1f) why.Add("CC_Life_Frail_Age".Translate(((int)age).Named("AGE"), LifeRiskMath.AgeFrailty(age).ToStringPercent().Named("SHARE")).Resolve());
                if (LifeRiskMath.ChildFrailty(age) < 1f) why.Add("CC_Life_Frail_Child".Translate(LifeRiskMath.ChildFrailty(age).ToStringPercent().Named("SHARE")).Resolve());
                if (LifeRiskMath.HungerFrailty(hunger) < 1f) why.Add("CC_Life_Frail_Hunger".Translate(LifeRiskMath.HungerFrailty(hunger).ToStringPercent().Named("SHARE")).Resolve());
                if (LifeRiskMath.HurtFrailty(health) < 1f) why.Add("CC_Life_Frail_Hurt".Translate(LifeRiskMath.HurtFrailty(health).ToStringPercent().Named("SHARE")).Resolve());
            }
            return f;
        }

        /// <summary>For a serious illness's letter: the frail fight it slower, and what saves them. Null for anyone fit.</summary>
        public static string FrailNote(Pawn p)
        {
            var why = new List<string>();
            float f = Frailty(p, why);
            if (f >= 0.9f || why.Count == 0) return null;
            return "CC_Life_FrailNote".Translate(p.Named("PAWN"), f.ToStringPercent().Named("SHARE"), string.Join(", ", why).Named("WHY")).Resolve();
        }

        // ---------------- Wounds left untended ----------------

        private static readonly SimpleCurve SeverityCurve = new SimpleCurve { new CurvePoint(1f, 0.1f), new CurvePoint(12f, 1f) };

        /// <summary>How much likelier an untended wound turns bad where they lie: a dirty room, or out in the open.</summary>
        public static float DirtWhere(Pawn p)
        {
            if (p == null || !p.Spawned) return LifeRiskMath.DirtFactor(0f, true);
            var room = p.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return LifeRiskMath.DirtFactor(0f, true);
            return LifeRiskMath.DirtFactor(room.GetStat(RoomStatDefOf.Cleanliness), false);
        }

        /// <summary>
        /// The game's roll for an infection in an untended wound has failed: one more, so the whole is as likely as the
        /// dirt where they lie says. True when the wound turned bad.
        /// </summary>
        public static bool DirtyWound(Hediff wound)
        {
            var p = wound?.pawn;
            if (p == null || p.Dead || p.Faction != Faction.OfPlayer || !p.RaceProps.Humanlike || wound.IsTended()) return false;
            if (p.health.immunity.DiseaseContractChanceFactor(HediffDefOf.WoundInfection, wound.Part) <= 0.001f || p.health.hediffSet.HasRegeneration) return false;
            float dirt = DirtWhere(p);
            float chance = SeverityCurve.Evaluate(wound.Severity) * (Find.Storyteller?.difficulty?.playerPawnInfectionChanceFactor ?? 1f);
            if (!Rand.Chance(LifeRiskMath.ExtraInfectionChance(chance, dirt))) return false;
            p.health.AddHediff(HediffDefOf.WoundInfection, wound.Part);
            Messages.Message("CC_Life_DirtyWound".Translate(p.Named("PAWN"), (wound.Part?.Label ?? wound.Label).Named("PART")).Resolve(), new LookTargets(p),
                MessageTypeDefOf.NegativeHealthEvent, false);
            return true;
        }
    }

    /// <summary>
    /// The frail fight an illness slower (LifeRisks.Frailty): a part of the game's immunity gain speed, shown in its
    /// breakdown. Added to the stat when the game starts (<see cref="LifeRisksStartup"/>).
    /// </summary>
    public class StatPart_Frailty : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!(req.Thing is Pawn p)) return;
            float f = LifeRisks.Frailty(p);
            if (f < 1f) val *= f;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is Pawn p)) return null;
            var why = new List<string>();
            float f = LifeRisks.Frailty(p, why);
            if (f >= 1f || why.Count == 0) return null;
            return "CC_Life_FrailStat".Translate().Resolve() + ": x" + f.ToStringPercent() + "\n  " + string.Join("\n  ", why);
        }
    }

    /// <summary>Puts <see cref="StatPart_Frailty"/> on the game's immunity gain speed, once, when the game starts.</summary>
    [StaticConstructorOnStartup]
    public static class LifeRisksStartup
    {
        static LifeRisksStartup()
        {
            try
            {
                var stat = StatDefOf.ImmunityGainSpeed;
                if (stat == null) return;
                stat.parts ??= new List<StatPart>();
                if (!stat.parts.Any(x => x is StatPart_Frailty)) stat.parts.Add(new StatPart_Frailty { parentStat = stat });
            }
            catch (Exception e)
            {
                Log.Warning("[Rimfolk] The frail can't be made to fight illness slower: " + e.Message);
            }
        }
    }

    /// <summary>A wound left untended in the dirt is likelier to turn bad (LifeRisks.DirtyWound), after the game's own roll fails.</summary>
    [HarmonyPatch(typeof(HediffComp_Infecter), "CheckMakeInfection")]
    public static class Patch_DirtyWound
    {
        public static void Postfix(HediffComp_Infecter __instance, ref int ___ticksUntilInfect)
        {
            // -3: the game rolled and no infection came.
            if (___ticksUntilInfect != -3 || !LifeRisks.DirtyWoundsOn || __instance?.parent == null || __instance.fromScaria) return;
            try
            {
                if (LifeRisks.DirtyWound(__instance.parent)) ___ticksUntilInfect = -4;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dirty wound", e);
            }
        }
    }

    /// <summary>The colony catches serious illness more often than it needs to: crowded sleeping, a dirty kitchen.</summary>
    public class AdvisorWorker_SicknessRisk : AdvisorSignalWorker
    {
        public override IEnumerable<AdvisorSignal> Evaluate(Map map)
        {
            if (!LifeRisks.CrowdingOn) yield break;
            var r = LifeRisks.Sickness(map);
            if (r.factor < LifeRiskMath.SicknessWorthSaying) yield break;
            var why = new List<string>();
            if (r.crowded > 0) why.Add("CC_Adv_Sickness_Crowded".Translate(r.crowded, r.people).Resolve());
            if (r.kitchen.HasValue && LifeRiskMath.KitchenFactor(r.kitchen.Value) > 1f) why.Add("CC_Adv_Sickness_Kitchen".Translate(r.kitchen.Value.ToString("0.0")).Resolve());
            if (LifeRiskMath.SizeFactor(r.people) > 1.1f) why.Add("CC_Adv_Sickness_Size".Translate(r.people).Resolve());
            var s = Signal(0.2f + 0.3f * Math.Min(1f, (r.factor - LifeRiskMath.SicknessWorthSaying) / (LifeRiskMath.MaxSicknessFactor - LifeRiskMath.SicknessWorthSaying)),
                "CC_Adv_Sickness_Title".Translate(), "CC_Adv_Sickness_Detail".Translate(r.factor.ToString("0.0"), string.Join("; ", why)).Resolve(), 0.4f);
            if (r.kitchenRoom != null && LifeRiskMath.KitchenFactor(r.kitchen ?? 0f) > 1f) s.look = new LookTargets(r.kitchenRoom.Cells.First(), map);
            s.key = "sickness";
            yield return s;
        }
    }
}
