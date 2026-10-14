using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A Rimfolk disease going round: the game's own human disease incident (victims picked by its colony-size rules,
    /// immunity and gene resistance respected, the medical tab and tending as for any disease), with what Rimfolk adds:
    /// the season or weather it needs, babies left out, at most one serious illness in the colony at a time, the
    /// setting switches, a letter with a doctor's note, and the first journal lines. It's fired by
    /// <see cref="AilmentTracker"/> on its own clock (no biome lists it, so the storyteller's disease rolls stay as
    /// they were); dev mode can fire it any time.
    /// </summary>
    public class IncidentWorker_Ailment : IncidentWorker_DiseaseHuman
    {
        private AilmentIncident Ext => def.GetModExtension<AilmentIncident>();

        private HediffCompProperties_Ailment Info => def.diseaseIncident?.CompProps<HediffCompProperties_Ailment>();

        private bool Serious => Info?.serious ?? false;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            var s = ColonyCouncilMod.Settings;
            if (def.diseaseIncident == null) return false;
            if (!parms.forced)
            {
                if (s == null || !s.ailments || (Serious && !s.seriousIllnesses)) return false;
                if (!(parms.target is Map map) || !Fits(map)) return false;
                if (Serious && !(AilmentTracker.Instance?.SeriousRoom(def.diseaseIncident) ?? false)) return false;
            }
            return base.CanFireNowSub(parms);
        }

        /// <summary>The season, the weather and the land are right for it.</summary>
        public bool Fits(Map map)
        {
            var ext = Ext;
            if (ext == null) return true;
            if (!ext.seasons.NullOrEmpty() && !ext.seasons.Contains(GenLocalDate.Season(map))) return false;
            float temp = map.mapTemperature.OutdoorTemp;
            if (temp > ext.maxOutdoorTemp || temp < ext.minOutdoorTemp) return false;
            if (ext.needsPlants && (map.Biome == null || map.Biome.plantDensity < 0.1f)) return false;
            return true;
        }

        /// <summary>How likely it is next to the other catching ailments right now.</summary>
        public float Weight(Map map)
        {
            var ext = Ext;
            float w = ext?.weight ?? 1f;
            if (ext != null && ext.coldSeasonFactor != 1f)
            {
                var season = GenLocalDate.Season(map);
                if (season == Season.Fall || season == Season.Winter || season == Season.PermanentWinter) w *= ext.coldSeasonFactor;
            }
            if (ext != null && ext.hotFactor != 1f && map.mapTemperature.OutdoorTemp >= 25f) w *= ext.hotFactor;
            return w;
        }

        protected override IEnumerable<Pawn> PotentialVictimCandidates(IIncidentTarget target)
        {
            // No baby is ever the one to catch these first; quest lodgers are the quest's business. Some illnesses come
            // to the old only (shingles), and some never twice (an appendix, measles).
            var ext = Ext;
            var info = Info;
            var tracker = AilmentTracker.Instance;
            float minAge = ext?.minAge ?? 0f;
            bool once = info?.once ?? false;
            return base.PotentialVictimCandidates(target).Where(p => p != null && !p.DevelopmentalStage.Baby() && !p.IsQuestLodger()
                && (minAge <= 0f || Flavor.HumanAge(p) >= minAge) && (!once || tracker == null || !tracker.HadBefore(p, def.diseaseIncident)));
        }

        protected override IEnumerable<Pawn> ActualVictims(IncidentParms parms)
        {
            var chosen = base.ActualVictims(parms).ToList();
            // A small colony and a small share still means someone (the serious ones take one person).
            if (chosen.Count == 0)
            {
                var one = PotentialVictims(parms.target).RandomElementWithFallback();
                if (one != null) chosen.Add(one);
            }
            var ext = Ext;
            var tracker = AilmentTracker.Instance;
            if (ext == null || !ext.sameVictims || tracker == null || chosen.Count == 0) return chosen;
            // The same people suffer it again: those who had it before go first, then the luck of the draw.
            var pool = PotentialVictims(parms.target).ToList();
            return pool.OrderByDescending(p => (tracker.HadBefore(p, def.diseaseIncident) ? 2f : 0f) + Rand.Value).Take(chosen.Count).ToList();
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var victims = ActualVictims(parms).ToList();
            if (victims.Count == 0) return false;
            var got = ApplyToPawns(victims, out string blocked);
            if (got.NullOrEmpty()) return false;
            var info = Info;
            var tracker = AilmentTracker.Instance;
            foreach (var p in got)
            {
                var h = p.health?.hediffSet?.GetFirstHediffOfDef(def.diseaseIncident);
                if (h == null) continue;
                // Those without an immunity race start somewhere in their course; the rest at the game's own start.
                if (h.TryGetComp<HediffComp_Immunizable>() == null && info != null) h.Severity = info.startSeverity.RandomInRange;
                h.TryGetComp<HediffComp_AilmentImmunizable>()?.EndIncubation();
                // The letter names it now, so it shows now: past any hidden first stage.
                if (!h.Visible) h.Severity = Math.Max(h.Severity, FirstVisibleSeverity(h.def));
                tracker?.Notify_Caught(p, h);
            }
            if (parms.sendLetter) SendLetter(parms, got, blocked);
            return true;
        }

        private static float FirstVisibleSeverity(HediffDef d)
        {
            if (d.stages != null)
                foreach (var s in d.stages)
                    if (s != null && s.becomeVisible)
                        return s.minSeverity + 0.001f;
            return d.initialSeverity;
        }

        private void SendLetter(IncidentParms parms, List<Pawn> got, string blocked)
        {
            var sb = new StringBuilder();
            foreach (var p in got) sb.AppendLine("  - " + p.LabelShortCap);
            string list = sb.ToString().TrimEnd();
            var ext = Ext;
            string text;
            try
            {
                string opening = (ext?.letter ?? def.letterText ?? "").Formatted(got[0].Named("PAWN")).Resolve();
                string closing = ext?.letterAfter.NullOrEmpty() == false ? ext.letterAfter.Formatted(got[0].Named("PAWN")).Resolve() : null;
                // A letter about one person that names them needs no list.
                bool named = got.Count == 1 && ext?.letter != null && ext.letter.Contains("{PAWN_");
                text = named ? opening : opening + "\n\n" + list;
                if (!closing.NullOrEmpty()) text += "\n\n" + closing;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ailment letter " + def.defName, e);
                text = list;
            }
            string note = AilmentTracker.DoctorsNote(def.diseaseIncident);
            if (!note.NullOrEmpty()) text += "\n\n" + note;
            if (!blocked.NullOrEmpty()) text += "\n\n" + blocked;
            string label = def.letterLabel.NullOrEmpty() ? def.diseaseIncident.LabelCap.ToString() : def.letterLabel;
            var letterDef = def.letterDef ?? (Serious ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent);
            SendStandardLetter(label, text, letterDef, parms, new LookTargets(got));
        }
    }
}
