using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A lasting setback's mood (a wish still waiting, a decree dragging on, a sick friend, quiet days...): felt as its
    /// group says (<see cref="Setbacks.Felt"/>), named from what the hour's look found ({OTHER_nameDef}, {WISH}...), and
    /// saying in the tooltip what caused it; what helps is its mood hint (Defs/MoodHintDefs/Council_MoodHints_Setbacks.xml).
    /// </summary>
    public class Thought_Setback : Thought_Situational, IExplainedThought
    {
        public override float MoodOffset() => Setbacks.Felt(pawn, def, base.MoodOffset());

        private ThoughtWorker_Setback Worker => def.Worker as ThoughtWorker_Setback;

        public override string LabelCap
        {
            get
            {
                try
                {
                    string raw = CurStage?.label;
                    if (raw.NullOrEmpty()) return base.LabelCap;
                    return raw.IndexOf('{') < 0 ? raw.CapitalizeFirst() : (Worker?.Fill(raw, pawn) ?? raw).CapitalizeFirst();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("setback label " + def?.defName, e);
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
                    return Worker?.Why(pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("setback cause " + def?.defName, e);
                    return null;
                }
            }
        }

        public override string Description
        {
            get
            {
                string raw = CurStage?.description;
                string text;
                try
                {
                    text = raw.NullOrEmpty() ? base.Description : raw.IndexOf('{') < 0 ? raw : Worker?.Fill(raw, pawn) ?? raw;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("setback description " + def?.defName, e);
                    text = base.Description;
                }
                return Causes.Append(text, Why);
            }
        }
    }

    /// <summary>The lasting setbacks' workers: each reads what the hour's look found (<see cref="SetbackTracker"/>).</summary>
    public abstract class ThoughtWorker_Setback : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            try
            {
                if (!Setbacks.On(def) || !Setbacks.Eligible(p)) return ThoughtState.Inactive;
                var s = Setbacks.Tracker?.StateOf(p);
                if (s == null) return ThoughtState.Inactive;
                int stage = Stage(s);
                if (stage < 0 || def.stages.NullOrEmpty()) return ThoughtState.Inactive;
                return ThoughtState.ActiveAtStage(Math.Min(stage, def.stages.Count - 1));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("setback " + def?.defName, e);
                return ThoughtState.Inactive;
            }
        }

        /// <summary>The stage to show (0 = the first), or -1 for none.</summary>
        protected abstract int Stage(SetbackState s);

        /// <summary>What caused it, for the tooltip and the Mood tab.</summary>
        protected abstract string Why(SetbackState s);

        /// <summary>The one it's about, for {OTHER_...}, if any.</summary>
        protected virtual Pawn Other(SetbackState s) => null;

        /// <summary>What it's about: the wish, decree, illness, nemesis or people its texts name.</summary>
        protected virtual string What(SetbackState s) => null;

        public string Why(Pawn p)
        {
            var s = Setbacks.Tracker?.StateOf(p);
            return s == null ? null : Why(s);
        }

        public string Fill(string raw, Pawn p)
        {
            var s = Setbacks.Tracker?.StateOf(p);
            var other = s != null ? Other(s) : null;
            string what = (s != null ? What(s) : null) ?? "?";
            // What it's about, under the name its texts use: the wish, the decree, the illness, the nemesis, the people.
            return raw.Formatted(p.Named("PAWN"), other != null ? other.Named("OTHER") : "?".Named("OTHER"), what.Named("WISH"), what.Named("DECREE"),
                what.Named("ILLNESS"), what.Named("NAME"), what.Named("FACTION")).Resolve();
        }
    }

    /// <summary>Their wish has waited while others' wishes, voiced later, came true.</summary>
    public class ThoughtWorker_SetbackWaiting : ThoughtWorker_Setback
    {
        protected override int Stage(SetbackState s) => s.waitStage;

        protected override string What(SetbackState s) => s.waitWish;

        protected override string Why(SetbackState s) =>
            "CC_Setback_Why_Waiting".Translate(s.waitWish ?? "?", Setbacks.Days(s.waitTicks), s.grantedSince).Resolve();
    }

    /// <summary>A decree they dislike, in force two days or more.</summary>
    public class ThoughtWorker_SetbackDecree : ThoughtWorker_Setback
    {
        protected override int Stage(SetbackState s) => s.dragStage;

        protected override string What(SetbackState s) => s.dragDecree;

        protected override string Why(SetbackState s) =>
            "CC_Setback_Why_Decree".Translate(s.dragDecree ?? "?", Setbacks.Days(s.dragTicks), Setbacks.Days(s.dragLeft)).Resolve();
    }

    /// <summary>A partner, family member or close friend seriously ill.</summary>
    public class ThoughtWorker_SetbackSickFriend : ThoughtWorker_Setback
    {
        protected override int Stage(SetbackState s) => s.sick != null && !s.sick.Dead ? s.sickStage : -1;

        protected override Pawn Other(SetbackState s) => s.sick;

        protected override string What(SetbackState s) => s.sickWhat;

        protected override string Why(SetbackState s) => s.sick == null ? null : "CC_Setback_Why_Sick".Translate(s.sick.Named("OTHER"), (s.sickWhat ?? "?").Named("WHAT")).Resolve();
    }

    /// <summary>Someone dear is with a caravan that's short of food or has someone down.</summary>
    public class ThoughtWorker_SetbackCaravan : ThoughtWorker_Setback
    {
        protected override int Stage(SetbackState s) => s.caravanOf != null && !s.caravanOf.Dead ? 0 : -1;

        protected override Pawn Other(SetbackState s) => s.caravanOf;

        protected override string Why(SetbackState s) => s.caravanOf == null ? null : "CC_Setback_Why_Caravan".Translate(s.caravanOf.Named("OTHER"), (s.caravanWhy ?? "").Named("WHY")).Resolve();
    }

    /// <summary>The nemesis who hurt them is still out there.</summary>
    public class ThoughtWorker_SetbackNemesis : ThoughtWorker_Setback
    {
        protected override int Stage(SetbackState s) => s.nemesis != null ? 0 : -1;

        protected override string What(SetbackState s) => s.nemesis;

        protected override string Why(SetbackState s) => "CC_Setback_Why_Nemesis".Translate(s.nemesis ?? "?", Setbacks.Days(s.nemesisTicks)).Resolve();
    }

    /// <summary>The colony owes a people a good turn (kind colonists).</summary>
    public class ThoughtWorker_SetbackDebt : ThoughtWorker_Setback
    {
        protected override int Stage(SetbackState s) => s.debtFaction != null ? 0 : -1;

        protected override string What(SetbackState s) => s.debtFaction;

        protected override string Why(SetbackState s) => "CC_Setback_Why_Debt".Translate(s.debtFaction ?? "?", s.debtWhat ?? "").Resolve();
    }

    /// <summary>Their bedroom is far worse than the best one at home.</summary>
    public class ThoughtWorker_SetbackEnvy : ThoughtWorker_Setback
    {
        protected override int Stage(SetbackState s) => s.envy && s.envyOf != null && !s.envyOf.Dead ? 0 : -1;

        protected override Pawn Other(SetbackState s) => s.envyOf;

        protected override string Why(SetbackState s) =>
            s.envyOf == null ? null : "CC_Setback_Why_Envy".Translate((s.envyMine ?? "?").Named("MINE"), s.envyOf.Named("OTHER"), (s.envyBest ?? "?").Named("BEST")).Resolve();
    }

    /// <summary>Long quiet days: little recreation and no gathering in a long while.</summary>
    public class ThoughtWorker_SetbackQuiet : ThoughtWorker_Setback
    {
        protected override int Stage(SetbackState s) => s.quietStage;

        protected override string Why(SetbackState s) => "CC_Setback_Why_Quiet".Translate(s.joyKinds, Setbacks.Days(s.quietTicks), s.joyNeeded).Resolve();
    }

    /// <summary>No research under way, for a colonist with a burning passion for it.</summary>
    public class ThoughtWorker_SetbackResearch : ThoughtWorker_Setback
    {
        protected override int Stage(SetbackState s) => s.researchIdle ? 0 : -1;

        protected override string Why(SetbackState s) => "CC_Setback_Why_Research".Translate(Setbacks.Days(s.researchIdleTicks)).Resolve();
    }
}
