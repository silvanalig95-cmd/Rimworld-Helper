using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A life goal: something a colonist has set their heart on doing before they die (master a craft, marry,
    /// raise a child, earn a name, teach someone everything they know). Long arcs, measured in seasons and years.
    /// </summary>
    public class AspirationDef : Def
    {
        public Type workerClass = typeof(AspirationWorker);
        public float weight = 1f;
        /// <summary>Who would want this (traits, passions, mood, office...).</summary>
        public DilemmaRole who;
        public bool children;
        /// <summary>Skill workers: this skill, or (empty) one they're passionate about.</summary>
        public SkillDef skill;
        /// <summary>Skill workers: the level to reach (at least current + gain).</summary>
        public int level = 15;
        public int gain = 4;
        /// <summary>Record workers: RecordDef name, and how much more of it.</summary>
        public string record;
        /// <summary>Record, Friends, Room, Years, ColonySize, Counter: the amount.</summary>
        public float amount = 1f;
        /// <summary>Counter workers: the event counted (graduated, peace, omen).</summary>
        public string counter;
        /// <summary>Office workers: this seat, or any.</summary>
        public OfficeDef office;
        /// <summary>The goal as a verb phrase, used in sentences: "master {SKILL}", "find someone to marry".</summary>
        [MustTranslate] public string goal;
        /// <summary>Journal, when they set their heart on it ({GOAL}).</summary>
        public ToneLines chosen = new ToneLines();
        /// <summary>Journal and the letter, when they achieve it ({GOAL}).</summary>
        public ToneLines achieved = new ToneLines();

        [Unsaved] private AspirationWorker worker;

        public AspirationWorker Worker
        {
            get
            {
                if (worker == null)
                {
                    worker = (AspirationWorker)Activator.CreateInstance(workerClass);
                    worker.def = this;
                }
                return worker;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (goal.NullOrEmpty()) yield return "life goal has no goal text";
            if (workerClass == null || !typeof(AspirationWorker).IsAssignableFrom(workerClass)) yield return "workerClass must be an AspirationWorker";
            if (achieved == null || !achieved.Any) yield return "life goal has no achieved lines";
        }
    }

    public class Aspiration : IExposable
    {
        public AspirationDef def;
        public Pawn pawn;
        public SkillDef skill;
        public float baseline;
        public float target;
        public int startedTick;
        public bool done;
        public int doneTick;

        public string Goal => def == null ? "" : def.goal.Formatted((skill?.label ?? "").Named("SKILL"), target.ToString("0").Named("COUNT")).Resolve();

        public float Progress => def == null ? 0f : Mathf.Clamp01(def.Worker.Progress(this));

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Defs.Look(ref skill, "skill");
            Scribe_Values.Look(ref baseline, "baseline");
            Scribe_Values.Look(ref target, "target");
            Scribe_Values.Look(ref startedTick, "startedTick");
            Scribe_Values.Look(ref done, "done");
            Scribe_Values.Look(ref doneTick, "doneTick");
        }
    }

    // ---------------- Workers ----------------

    public class AspirationWorker
    {
        public AspirationDef def;

        public virtual bool CanHave(Pawn p) => true;

        /// <summary>Sets the skill, baseline and target. False if it can't be set up for them after all.</summary>
        public virtual bool Init(Aspiration a) => true;

        public virtual float Progress(Aspiration a) => 0f;

        public virtual bool Done(Aspiration a) => Progress(a) >= 1f;

        public virtual bool StillPossible(Aspiration a) => true;

        public virtual string ProgressText(Aspiration a) => Progress(a).ToStringPercent();
    }

    /// <summary>Reach a skill level.</summary>
    public class AspirationWorker_Skill : AspirationWorker
    {
        private SkillDef Pick(Pawn p) => def.skill ?? p.skills?.skills
            .Where(s => !s.TotallyDisabled && s.passion != Passion.None && s.Level < 18)
            .RandomElementByWeightWithFallback(s => s.passion == Passion.Major ? 3f : 1f)?.def;

        public override bool CanHave(Pawn p)
        {
            var sk = def.skill;
            if (sk == null) return Pick(p) != null;
            var rec = p.skills?.GetSkill(sk);
            return rec != null && !rec.TotallyDisabled && rec.Level < Mathf.Min(20, def.level);
        }

        public override bool Init(Aspiration a)
        {
            a.skill = Pick(a.pawn);
            var rec = a.skill != null ? a.pawn.skills?.GetSkill(a.skill) : null;
            if (rec == null) return false;
            a.baseline = rec.Level;
            a.target = Mathf.Min(20, Mathf.Max(def.level, rec.Level + def.gain));
            return true;
        }

        private static SkillRecord Rec(Aspiration a) => a.skill != null ? a.pawn.skills?.GetSkill(a.skill) : null;

        public override float Progress(Aspiration a)
        {
            var rec = Rec(a);
            if (rec == null) return 0f;
            float span = Mathf.Max(1f, a.target - a.baseline);
            return (rec.Level + rec.XpProgressPercent - a.baseline) / span;
        }

        public override bool Done(Aspiration a) => (Rec(a)?.Level ?? 0) >= a.target;

        public override bool StillPossible(Aspiration a) => Rec(a) != null && !Rec(a).TotallyDisabled;

        public override string ProgressText(Aspiration a) => "CC_Asp_SkillProgress".Translate(a.skill?.label ?? "", Rec(a)?.Level ?? 0, a.target.ToString("0"));
    }

    /// <summary>Add to one of their records (kills, things built, meals cooked...).</summary>
    public class AspirationWorker_Record : AspirationWorker
    {
        private RecordDef Rec => DefDatabase<RecordDef>.GetNamedSilentFail(def.record);

        public override bool CanHave(Pawn p) => Rec != null && p.records != null;

        public override bool Init(Aspiration a)
        {
            a.baseline = a.pawn.records.GetValue(Rec);
            a.target = a.baseline + def.amount;
            return true;
        }

        private float Now(Aspiration a) => Rec != null && a.pawn.records != null ? a.pawn.records.GetValue(Rec) : a.baseline;

        public override float Progress(Aspiration a) => (Now(a) - a.baseline) / Mathf.Max(1f, a.target - a.baseline);

        public override string ProgressText(Aspiration a) => "CC_Asp_CountProgress".Translate((Now(a) - a.baseline).ToString("0"), (a.target - a.baseline).ToString("0"));
    }

    /// <summary>Marry.</summary>
    public class AspirationWorker_Spouse : AspirationWorker
    {
        private static bool Married(Pawn p) => p.relations != null && p.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Spouse && r.otherPawn != null && !r.otherPawn.Dead);

        public override bool CanHave(Pawn p)
        {
            var asexual = DefDatabase<TraitDef>.GetNamedSilentFail("Asexual");
            return !Married(p) && (asexual == null || p.story?.traits?.HasTrait(asexual) != true);
        }

        public override float Progress(Aspiration a)
        {
            if (Married(a.pawn)) return 1f;
            var p = a.pawn;
            if (p.relations != null && p.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Fiance && !r.otherPawn.Dead)) return 0.75f;
            return LovePartnerRelationUtility.ExistingLovePartner(p, false) != null ? 0.4f : 0f;
        }
    }

    /// <summary>Have a child (Biotech).</summary>
    public class AspirationWorker_Child : AspirationWorker
    {
        private static int Children(Pawn p) => p.relations?.Children.Count(c => !c.Dead) ?? 0;

        public override bool CanHave(Pawn p) => ModsConfig.BiotechActive && p.ageTracker != null && p.ageTracker.AgeBiologicalYears < 45;

        public override bool Init(Aspiration a)
        {
            a.baseline = Children(a.pawn);
            a.target = a.baseline + 1;
            return true;
        }

        public override float Progress(Aspiration a) => Children(a.pawn) >= a.target ? 1f : 0f;
    }

    /// <summary>Have this many real friends.</summary>
    public class AspirationWorker_Friends : AspirationWorker
    {
        private static int Count(Pawn p) => CouncilGameComponent.Instance?.social.ties.Count(t => t.kind == TieKind.Friends && t.announced && t.strength >= 45f && t.Involves(p)) ?? 0;

        public override bool CanHave(Pawn p) => ColonyCouncilMod.Settings.socialWeb && Count(p) < def.amount;

        public override bool Init(Aspiration a)
        {
            a.target = def.amount;
            return true;
        }

        public override float Progress(Aspiration a) => Count(a.pawn) / Mathf.Max(1f, a.target);

        public override string ProgressText(Aspiration a) => "CC_Asp_CountProgress".Translate(Count(a.pawn), a.target.ToString("0"));
    }

    /// <summary>Something counted by the mod: graduating a student, making peace with a rival, a dream that came true.</summary>
    public class AspirationWorker_Counter : AspirationWorker
    {
        public override bool CanHave(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return false;
            switch (def.counter)
            {
                case "peace": return comp.social.ties.Any(t => t.kind == TieKind.Rivals && t.announced && t.Involves(p));
                case "graduated": return ColonyCouncilMod.Settings.socialWeb && p.skills != null && p.skills.skills.Any(s => s.Level >= 10 && s.passion != Passion.None);
                case "omen": return ColonyCouncilMod.Settings.dreams && ColonyCouncilMod.Settings.dreamOmens;
                default: return true;
            }
        }

        public override bool Init(Aspiration a)
        {
            a.baseline = CouncilGameComponent.Instance.aspirations.Count(a.pawn, def.counter);
            a.target = a.baseline + def.amount;
            return true;
        }

        public override float Progress(Aspiration a) => (CouncilGameComponent.Instance.aspirations.Count(a.pawn, def.counter) - a.baseline) / Mathf.Max(1f, a.target - a.baseline);
    }

    /// <summary>Hold a council office (one in particular, or any).</summary>
    public class AspirationWorker_Office : AspirationWorker
    {
        public override bool CanHave(Pawn p) => ColonyCouncilMod.Settings.offices && !p.DevelopmentalStage.Juvenile() && CouncilGameComponent.Instance?.offices.OfficeOf(p) == null;

        public override float Progress(Aspiration a)
        {
            var held = CouncilGameComponent.Instance?.offices.OfficeOf(a.pawn);
            return held != null && (def.office == null || held == def.office) ? 1f : 0f;
        }
    }

    /// <summary>A bedroom of their own at least this impressive.</summary>
    public class AspirationWorker_Room : AspirationWorker
    {
        private static float Impressiveness(Pawn p)
        {
            var bed = p.ownership?.OwnedBed;
            if (bed == null || !bed.Spawned) return 0f;
            var room = bed.GetRoom();
            if (room == null || room.PsychologicallyOutdoors || room.Role != RoomRoleDefOf.Bedroom) return 0f;
            return room.GetStat(RoomStatDefOf.Impressiveness);
        }

        public override bool CanHave(Pawn p) => Impressiveness(p) < def.amount;

        public override bool Init(Aspiration a)
        {
            a.target = def.amount;
            return true;
        }

        public override float Progress(Aspiration a) => Impressiveness(a.pawn) / Mathf.Max(1f, a.target);

        public override string ProgressText(Aspiration a) => "CC_Asp_RoomProgress".Translate(Impressiveness(a.pawn).ToString("0"), a.target.ToString("0"));
    }

    /// <summary>Earn a name (an epithet).</summary>
    public class AspirationWorker_Epithet : AspirationWorker
    {
        private static bool Named(Pawn p) => CouncilGameComponent.Instance?.epithets.Of(p).Any() ?? false;

        public override bool CanHave(Pawn p) => ColonyCouncilMod.Settings.epithets && !Named(p);

        public override float Progress(Aspiration a) => Named(a.pawn) ? 1f : 0f;
    }

    /// <summary>See this many more years with the colony.</summary>
    public class AspirationWorker_Years : AspirationWorker
    {
        private static float Years(Pawn p) => (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) / GenDate.TicksPerYear;

        public override bool Init(Aspiration a)
        {
            a.baseline = Years(a.pawn);
            a.target = Mathf.Floor(a.baseline) + def.amount;
            return true;
        }

        public override float Progress(Aspiration a) => (Years(a.pawn) - a.baseline) / Mathf.Max(0.1f, a.target - a.baseline);

        public override string ProgressText(Aspiration a) => "CC_Asp_YearsProgress".Translate(Years(a.pawn).ToString("0.#"), a.target.ToString("0"));
    }

    /// <summary>See the colony grow to this many people.</summary>
    public class AspirationWorker_ColonySize : AspirationWorker
    {
        private static int Size() => PawnsFinder.AllMaps_FreeColonists.Count(p => !p.IsQuestLodger());

        public override bool CanHave(Pawn p) => Size() < def.amount;

        public override bool Init(Aspiration a)
        {
            a.target = Mathf.Max(def.amount, Size() + 3);
            return true;
        }

        public override float Progress(Aspiration a) => Size() / Mathf.Max(1f, a.target);

        public override string ProgressText(Aspiration a) => "CC_Asp_CountProgress".Translate(Size(), a.target.ToString("0"));
    }

    // ---------------- Tracker ----------------

    public class AspirationTracker : IExposable
    {
        public List<Aspiration> all = new List<Aspiration>();
        private Dictionary<string, int> counters = new Dictionary<string, int>();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public Aspiration Active(Pawn p) => p == null ? null : all.FirstOrDefault(a => a.pawn == p && !a.done);

        public IEnumerable<Aspiration> Fulfilled(Pawn p) => all.Where(a => a.pawn == p && a.done);

        public int Count(Pawn p, string key) => p != null && key != null && counters.TryGetValue(p.ThingID + "|" + key, out int n) ? n : 0;

        /// <summary>Something a life goal might count happened to them.</summary>
        public void Notify(Pawn p, string key)
        {
            if (p == null || key == null) return;
            string k = p.ThingID + "|" + key;
            counters[k] = (counters.TryGetValue(k, out int n) ? n : 0) + 1;
        }

        private static bool Eligible(Pawn p) => p != null && !p.Dead && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Baby() && !p.IsSlave;

        public void Daily(CouncilGameComponent comp)
        {
            // The dead and the departed.
            foreach (var a in all.Where(a => !a.done && (a.pawn == null || a.pawn.Dead || !a.pawn.IsColonist)).ToList())
            {
                all.Remove(a);
                if (a.pawn != null && a.pawn.Dead && a.def != null && S.aspirations) Mourn(comp, a);
            }
            all.RemoveAll(a => a.def == null || (a.done && (a.pawn == null || a.pawn.Dead) && Find.TickManager.TicksGame - a.doneTick > GenDate.TicksPerYear * 3));
            if (!S.aspirations) return;

            foreach (var p in PawnsFinder.AllMaps_FreeColonists.Where(Eligible).ToList())
            {
                var a = Active(p);
                if (a == null)
                {
                    TryChoose(comp, p);
                    continue;
                }
                try
                {
                    if (a.def.Worker.Done(a)) Fulfil(comp, a);
                    else if (!a.def.Worker.StillPossible(a))
                    {
                        all.Remove(a);
                        comp.journals.Write(p, "Council_Journal_GoalLost", null, a.Goal.Named("GOAL"));
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("life goal " + a.def.defName, e);
                }
            }
        }

        private void TryChoose(CouncilGameComponent comp, Pawn p)
        {
            int inColony = p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0;
            if (inColony < GenDate.TicksPerDay * 3) return;
            var last = all.Where(x => x.pawn == p && x.done).OrderByDescending(x => x.doneTick).FirstOrDefault();
            if (last != null && Find.TickManager.TicksGame - last.doneTick < GenDate.TicksPerDay * 20) return;
            if (!Rand.Chance(0.2f)) return;
            var done = new HashSet<AspirationDef>(Fulfilled(p).Select(x => x.def));
            var options = DefDatabase<AspirationDef>.AllDefsListForReading
                .Where(d => !done.Contains(d) && DilemmaRole.Fits(d.who, p, d.children) && SafeCanHave(d, p)).ToList();
            if (options.Count == 0) return;
            var def = options.RandomElementByWeight(d => d.weight * (d.who?.passion != null || d.who?.anyTrait != null ? 2f : 1f));
            var a = new Aspiration { def = def, pawn = p, startedTick = Find.TickManager.TicksGame };
            if (!def.Worker.Init(a)) return;
            all.Add(a);
            string line = Line(def.chosen, p, a);
            if (!line.NullOrEmpty()) comp.journals.WriteText(p, line);
            string text = "CC_Asp_Chosen".Translate(p.Named("PAWN"), a.Goal.Named("GOAL")).Resolve();
            Messages.Message(text + (line.NullOrEmpty() ? "" : " \"" + line + "\""), p, MessageTypeDefOf.SilentInput, false);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Note, text, p));
        }

        private static bool SafeCanHave(AspirationDef d, Pawn p)
        {
            try
            {
                return d.Worker.CanHave(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("life goal " + d.defName, e);
                return false;
            }
        }

        private static string Line(ToneLines lines, Pawn p, Aspiration a)
        {
            if (lines == null || !lines.Any) return null;
            string raw = lines.Pick(ToneUtility.ToneFor(p), Flavor.TagsFor(p));
            return raw.NullOrEmpty() ? null : raw.Formatted(p.Named("PAWN"), a.Goal.Named("GOAL"), (a.skill?.label ?? "").Named("SKILL")).Resolve();
        }

        private void Fulfil(CouncilGameComponent comp, Aspiration a)
        {
            var p = a.pawn;
            a.done = true;
            a.doneTick = Find.TickManager.TicksGame;
            string quote = Line(a.def.achieved, p, a);
            string text = "CC_Asp_FulfilledText".Translate(p.Named("PAWN"), a.Goal.Named("GOAL")).Resolve();
            if (!quote.NullOrEmpty()) text += "\n\n\"" + quote + "\"";
            Find.LetterStack.ReceiveLetter("CC_Asp_FulfilledLabel".Translate(p.LabelShort), text, LetterDefOf.PositiveEvent, p);
            if (!quote.NullOrEmpty()) comp.journals.WriteText(p, quote);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Asp_FulfilledText".Translate(p.Named("PAWN"), a.Goal.Named("GOAL")).Resolve(), p));
            comp.annals.Notify_Aspiration(p, a.Goal);
            CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_Asp_Fulfilled);
            comp.standings.Change(p, 10f, "CC_Log_Aspiration".Translate(a.Goal));
            if (Rand.Chance(0.5f)) p.mindState?.inspirationHandler?.TryStartInspiration(p.mindState.inspirationHandler.GetRandomAvailableInspirationDef(), null, true);
            // The people who love them are glad.
            foreach (var f in PawnsFinder.AllMaps_FreeColonists.Where(f => f != p && f.relations != null && (f.relations.OpinionOf(p) >= 40 || comp.social.Warmth(f, p) >= 40f)))
                CouncilGameComponent.GiveThought(f, CouncilDefOf.Council_HappyForThem, p);
        }

        /// <summary>They died before they got there: the chronicle says so, and their friends write about it.</summary>
        private void Mourn(CouncilGameComponent comp, Aspiration a)
        {
            var p = a.pawn;
            string text = "CC_Asp_DiedBefore".Translate(p.Named("PAWN"), a.Goal.Named("GOAL")).Resolve();
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Note, text, null));
            foreach (var f in PawnsFinder.AllMaps_FreeColonists.Where(f => f != p && f.relations != null && (f.relations.OpinionOf(p) >= 30 || comp.social.Warmth(f, p) >= 35f)).Take(4))
                comp.journals.Write(f, "Council_Journal_DiedDreaming", null, p.Named("OTHER"), a.Goal.Named("GOAL"));
            lastMourned[p] = a.Goal;
        }

        /// <summary>The goal someone died before reaching, for their eulogy.</summary>
        [Unsaved] public Dictionary<Pawn, string> lastMourned = new Dictionary<Pawn, string>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref all, "all", LookMode.Deep);
            Scribe_Collections.Look(ref counters, "counters", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (all == null) all = new List<Aspiration>();
                all.RemoveAll(a => a == null || a.def == null || a.pawn == null);
                if (counters == null) counters = new Dictionary<string, int>();
            }
        }
    }

    /// <summary>"Within reach": a small lift while a life goal is nearly done.</summary>
    public class ThoughtWorker_GoalInReach : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var a = CouncilGameComponent.Instance?.aspirations.Active(p);
            if (a == null || !ColonyCouncilMod.Settings.aspirations) return ThoughtState.Inactive;
            return a.Progress >= 0.75f ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }
}
