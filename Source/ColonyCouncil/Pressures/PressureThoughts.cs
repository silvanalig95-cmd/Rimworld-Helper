using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// An everyday pressure's mood: the bad side follows the "Life's ups and downs" setting and the colonist's standards
    /// (never less than a whole point); the good side is a small reward like the council's, so it follows the reward
    /// strength and standards too. The label and description name the meal, work, person, room or thing, and the
    /// tooltip says what caused it; what helps is its mood hint (Defs/MoodHintDefs/Council_MoodHints_Pressures.xml).
    /// </summary>
    public class Thought_Pressure : Thought_Situational, IExplainedThought
    {
        public override float MoodOffset()
        {
            float m = base.MoodOffset();
            if (m < 0f) return Pressures.Bad(pawn, m);
            if (m > 0f) return m * SmallJoys.LiveScale(pawn);
            return m;
        }

        public override string LabelCap
        {
            get
            {
                try
                {
                    // From the stage's own label: the game's has already dropped the {FOOD}-style names by now.
                    string raw = def.Worker is ThoughtWorker_Pressure w ? w.Label(pawn, CurStage?.label) : CurStage?.label;
                    if (raw.NullOrEmpty() || raw.IndexOf('{') < 0) return raw.NullOrEmpty() ? base.LabelCap : raw.CapitalizeFirst();
                    return Pressures.Fill(raw, pawn).CapitalizeFirst();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pressure label " + def?.defName, e);
                    return base.LabelCap;
                }
            }
        }

        public string Why
        {
            get
            {
                try
                {
                    return (def.Worker as ThoughtWorker_Pressure)?.Why(pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pressure cause " + def?.defName, e);
                    return null;
                }
            }
        }

        public override string Description
        {
            get
            {
                string raw = def.Worker is ThoughtWorker_Pressure w ? w.Describe(pawn, CurStage?.description) : CurStage?.description;
                string text;
                try
                {
                    text = raw.NullOrEmpty() ? base.Description : Pressures.Fill(raw, pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("pressure description " + def?.defName, e);
                    text = base.Description;
                }
                return Causes.Append(text, Why);
            }
        }
    }

    /// <summary>The everyday pressures' thought workers: each reads what the hourly look found (<see cref="PressureTracker"/>).</summary>
    public abstract class ThoughtWorker_Pressure : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            try
            {
                if (!Pressures.On || !Pressures.Eligible(p)) return ThoughtState.Inactive;
                var t = Pressures.Tracker;
                if (t == null) return ThoughtState.Inactive;
                int stage = Stage(p, t);
                if (stage < 0 || def.stages.NullOrEmpty()) return ThoughtState.Inactive;
                return ThoughtState.ActiveAtStage(Math.Min(stage, def.stages.Count - 1));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pressure " + def?.defName, e);
                return ThoughtState.Inactive;
            }
        }

        /// <summary>The stage to show (0 = the first), or -1 for none.</summary>
        protected abstract int Stage(Pawn p, PressureTracker t);

        /// <summary>The stage's label, or another one that fits better right now.</summary>
        public virtual string Label(Pawn p, string stageLabel) => stageLabel;

        /// <summary>The stage's description, or another one that fits better right now.</summary>
        public virtual string Describe(Pawn p, string stageDescription) => stageDescription;

        /// <summary>The cause, for the tooltip and the Mood tab.</summary>
        public abstract string Why(Pawn p);
    }

    /// <summary>The same meal again and again.</summary>
    public class ThoughtWorker_SameMeal : ThoughtWorker_Pressure
    {
        protected override int Stage(Pawn p, PressureTracker t) => (t.Peek(p)?.foodStage ?? 0) - 1;

        public override string Why(Pawn p)
        {
            var rec = Pressures.Tracker?.Peek(p);
            if (rec?.foodKind == null) return null;
            string food = Find.ActiveLanguageWorker.Pluralize(Pressures.FoodLabel(rec.foodKind), rec.foodCount);
            return "CC_Pressure_Why_SameMeal".Translate(rec.foodCount, rec.foodOf, food).Resolve();
        }
    }

    /// <summary>A varied table: three or more different foods in the last two days.</summary>
    public class ThoughtWorker_VariedTable : ThoughtWorker_Pressure
    {
        protected override int Stage(Pawn p, PressureTracker t) => t.Peek(p)?.varied == true ? 0 : -1;

        public override string Why(Pawn p)
        {
            var rec = Pressures.Tracker?.Peek(p);
            return rec == null ? null : "CC_Pressure_Why_VariedTable".Translate(rec.kindsLately).Resolve();
        }
    }

    /// <summary>The same work, day after day.</summary>
    public class ThoughtWorker_SameWork : ThoughtWorker_Pressure
    {
        protected override int Stage(Pawn p, PressureTracker t) => (t.Peek(p)?.workStage ?? 0) - 1;

        public override string Why(Pawn p)
        {
            var rec = Pressures.Tracker?.Peek(p);
            return rec == null ? null : "CC_Pressure_Why_SameWork".Translate(Pressures.WorkLabel(rec.streakWork), rec.streakDays).Resolve();
        }
    }

    /// <summary>Missing a partner or a close friend who has been away for days.</summary>
    public class ThoughtWorker_Missing : ThoughtWorker_Pressure
    {
        protected override int Stage(Pawn p, PressureTracker t)
        {
            var rec = t.Peek(p);
            if (rec?.missed == null || rec.missed.Dead) return -1;
            return rec.missedStage - 1;
        }

        public override string Why(Pawn p)
        {
            var rec = Pressures.Tracker?.Peek(p);
            if (rec?.missed == null) return null;
            string key = rec.missedPartner ? "CC_Pressure_Why_MissingPartner" : "CC_Pressure_Why_MissingFriend";
            return key.Translate(rec.missed.Named("OTHER"), TextUtil.DaysString(rec.missedTicks).Named("TIME")).Resolve();
        }
    }

    /// <summary>A dining or rec room fuller than it can hold, felt for half a day afterwards.</summary>
    public class ThoughtWorker_Crowded : ThoughtWorker_Pressure
    {
        protected override int Stage(Pawn p, PressureTracker t)
        {
            var rec = t.Peek(p);
            return PressureTracker.CrowdedLately(rec) ? rec.crowdLevel - 1 : -1;
        }

        public override string Why(Pawn p)
        {
            var rec = Pressures.Tracker?.Peek(p);
            if (rec == null) return null;
            string room = rec.crowdRoom ?? "CC_Pressure_RoomFallback".Translate().ToString();
            return rec.crowdSeats >= 0
                ? "CC_Pressure_Why_CrowdedSeats".Translate(rec.crowdPeople, room, rec.crowdSeats).Resolve()
                : "CC_Pressure_Why_Crowded".Translate(rec.crowdPeople, room, rec.crowdCells).Resolve();
        }
    }

    /// <summary>Half-built frames and old plans left standing in the home area.</summary>
    public class ThoughtWorker_Unfinished : ThoughtWorker_Pressure
    {
        protected override int Stage(Pawn p, PressureTracker t)
        {
            if (Pressures.Gentle || !p.Spawned || p.Map == null || !p.Map.IsPlayerHome || Pressures.SparedUnfinished(p)) return -1;
            var u = t.UnfinishedOn(p.Map);
            if (u == null || u.stage <= 0) return -1;
            int stage = Pressures.MindsUnfinished(p) ? Math.Min(3, u.stage + 1) : u.stage;
            return stage - 1;
        }

        public override string Label(Pawn p, string stageLabel) => PlansOnly(p) ? "CC_Pressure_PlansLabel".Translate().ToString() : stageLabel;

        public override string Describe(Pawn p, string stageDescription) => PlansOnly(p) ? "CC_Pressure_PlansDesc".Translate().ToString() : stageDescription;

        /// <summary>Plans nobody has started aren't half-built yet.</summary>
        private static bool PlansOnly(Pawn p)
        {
            var u = p?.Map != null ? Pressures.Tracker?.UnfinishedOn(p.Map) : null;
            return u != null && u.frames == 0 && u.blueprints > 0;
        }

        public override string Why(Pawn p)
        {
            var u = p?.Map != null ? Pressures.Tracker?.UnfinishedOn(p.Map) : null;
            return u == null ? null : "CC_Pressure_Why_Unfinished".Translate(u.frames, u.blueprints).Resolve();
        }
    }
}
