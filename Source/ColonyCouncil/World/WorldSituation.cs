using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The people, places and words of one world situation. It extends the world events' context, so every dilemma and
    /// event effect works in a situation's answers too; it adds {OTHER}, {NAME}, {TITLE}, {ITEM} and {DAYS}.
    /// </summary>
    public class SituationContext : HappeningContext
    {
        /// <summary>The situation it belongs to (letters keep a copy of the context and find their situation by it).</summary>
        public int situationId;
        /// <summary>A second person ({OTHER}): a rival claimant, a hunter, a captain.</summary>
        public Pawn other;
        /// <summary>The situation's name ({NAME}).</summary>
        public string name;
        public string item;
        public string days;

        public SituationContext Copy() => (SituationContext)MemberwiseClone();

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((other != null ? other.Named("OTHER") : "?".Named("OTHER")));
            args.Add((name ?? "").Named("NAME"));
            args.Add((faction?.LeaderTitle ?? "").Named("TITLE"));
            args.Add((item ?? "").Named("ITEM"));
            args.Add((days ?? "").Named("DAYS"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref situationId, "situationId");
            Scribe_References.Look(ref other, "other");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref item, "item");
            Scribe_Values.Look(ref days, "days");
        }
    }

    /// <summary>A beat waiting for its day.</summary>
    public class BeatState : IExposable
    {
        public string key;
        public int due;
        public int times;
        public int postponed;

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref due, "due");
            Scribe_Values.Look(ref times, "times");
            Scribe_Values.Look(ref postponed, "postponed");
        }
    }

    /// <summary>One world situation as it runs: who it's about, how long it lasts, what the colony has done.</summary>
    public class WorldSituation : IExposable
    {
        public int id;
        public WorldSituationDef def;
        public int startTick;
        public int endTick;
        public SituationContext ctx = new SituationContext();
        public List<string> flags = new List<string>();
        /// <summary>How much the colony has taken part (each answer adds its help).</summary>
        public int help;
        /// <summary>The colony's part, told when it ends ("sent medicine to Kettleford").</summary>
        public List<string> deeds = new List<string>();
        public List<BeatState> beats = new List<BeatState>();
        /// <summary>An answer brought the end nearer: this ending, at this tick.</summary>
        public string pendingEnding;
        public int pendingTick = -1;
        public bool ended;
        public string endingKey;
        /// <summary>Colonists it hurt (the warlord's raids): they feel how it ends.</summary>
        public List<Pawn> sufferers = new List<Pawn>();
        /// <summary>People the situation made and keeps in the world until it ends (claimants, a healer).</summary>
        public List<Pawn> held = new List<Pawn>();
        /// <summary>Numbers a worker keeps (raids beaten off, the day the last one came).</summary>
        public Dictionary<string, int> numbers = new Dictionary<string, int>();

        public WorldSituationWorker Worker => def?.Worker;

        private static int Now => Find.TickManager.TicksGame;

        public bool Has(string flag) => !flag.NullOrEmpty() && flags.Contains(flag);

        public void Set(string flag)
        {
            if (!flag.NullOrEmpty() && !flags.Contains(flag)) flags.Add(flag);
            WorldSituations.Changed();
        }

        public void Clear(string flag)
        {
            if (flags.Remove(flag)) WorldSituations.Changed();
        }

        public bool HasAll(List<string> list) => list.NullOrEmpty() || list.All(Has);

        public bool HasNone(List<string> list) => list.NullOrEmpty() || !list.Any(Has);

        public int Num(string key) => numbers.TryGetValue(key, out int v) ? v : 0;

        public void SetNum(string key, int v) => numbers[key] = v;

        public void AddNum(string key, int v = 1) => numbers[key] = Num(key) + v;

        public float DaysIn => (Now - startTick) / (float)GenDate.TicksPerDay;

        public int TicksLeft => Mathf.Max(0, (pendingTick > 0 ? Mathf.Min(pendingTick, endTick) : endTick) - Now);

        public int Duration => Mathf.Max(1, endTick - startTick);

        public string Label => (def?.LabelCap.ToString() ?? "?") + (ctx.name.NullOrEmpty() ? "" : ": " + ctx.name);

        /// <summary>Keeps someone in the world while the situation needs them.</summary>
        public void Hold(Pawn p)
        {
            if (p == null || p.Dead || p.Spawned) return;
            if (!held.Contains(p)) held.Add(p);
            if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
            else Find.WorldPawns.ForcefullyKeptPawns.Add(p);
        }

        /// <summary>Lets go of everyone it kept (a nemesis, or someone a Rimfolk event still needs, stays held by them).</summary>
        public void ReleaseAll()
        {
            var comp = CouncilGameComponent.Instance;
            foreach (var p in held.Where(p => p != null))
            {
                if (comp != null && (comp.nemeses.IsNemesis(p) || comp.happenings.Holds(p))) continue;
                if (WorldSituations.Instance?.HoldsElsewhere(this, p) ?? false) continue;
                Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            }
            held.Clear();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref endTick, "endTick");
            Scribe_Deep.Look(ref ctx, "ctx");
            Scribe_Collections.Look(ref flags, "flags", LookMode.Value);
            Scribe_Values.Look(ref help, "help");
            Scribe_Collections.Look(ref deeds, "deeds", LookMode.Value);
            Scribe_Collections.Look(ref beats, "beats", LookMode.Deep);
            Scribe_Values.Look(ref pendingEnding, "pendingEnding");
            Scribe_Values.Look(ref pendingTick, "pendingTick", -1);
            Scribe_Values.Look(ref ended, "ended");
            Scribe_Values.Look(ref endingKey, "endingKey");
            Scribe_Collections.Look(ref sufferers, "sufferers", LookMode.Reference);
            Scribe_Collections.Look(ref held, "held", LookMode.Reference);
            Scribe_Collections.Look(ref numbers, "numbers", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (ctx == null) ctx = new SituationContext();
                ctx.situationId = id;
                if (flags == null) flags = new List<string>();
                if (deeds == null) deeds = new List<string>();
                if (beats == null) beats = new List<BeatState>();
                beats.RemoveAll(b => b == null || b.key.NullOrEmpty());
                if (sufferers == null) sufferers = new List<Pawn>();
                sufferers.RemoveAll(p => p == null);
                if (held == null) held = new List<Pawn>();
                held.RemoveAll(p => p == null);
                if (numbers == null) numbers = new Dictionary<string, int>();
            }
        }
    }

    /// <summary>What's left of a world situation once it's over: who it was about, how it ended, whether the colony helped.</summary>
    public class SituationRecord : IExposable
    {
        public string defName;
        public string label;
        public Faction faction;
        public string ending;
        public bool helped;
        public int help;
        public int startTick;
        public int endTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref label, "label");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref ending, "ending");
            Scribe_Values.Look(ref helped, "helped");
            Scribe_Values.Look(ref help, "help");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref endTick, "endTick");
        }
    }

    /// <summary>A world situation's question for the colony.</summary>
    public class ChoiceLetter_Situation : ChoiceLetter
    {
        public SituationContext ctx;
        public string beatKey;
        public bool resolved;

        public WorldSituation Situation => WorldSituations.Instance?.Get(ctx?.situationId ?? -1);

        public SituationBeat Beat => Situation?.def?.Beat(beatKey);

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var s = Situation;
                var beat = Beat;
                if (ArchivedOnly || resolved || s == null || s.ended || beat == null || beat.options.NullOrEmpty())
                {
                    yield return Option_Close;
                    yield break;
                }
                for (int i = 0; i < beat.options.Count; i++)
                {
                    int index = i;
                    var o = beat.options[i];
                    if (!s.Worker.Offered(s, beat, i)) continue;
                    var opt = new DiaOption(ctx.Format(o.label));
                    // The default answer is always possible, so the letter can never trap the player.
                    string why = i == beat.defaultOption ? null : s.Worker.Unavailable(s, ctx, beat, i) ?? o.effects?.Select(e => e.Unavailable(ctx)).FirstOrDefault(r => r != null);
                    if (why != null) opt.Disable(why);
                    opt.action = () =>
                    {
                        WorldSituations.Instance?.Resolve(this, index, false);
                        Find.LetterStack.RemoveLetter(this);
                    };
                    opt.resolveTree = true;
                    yield return opt;
                }
                yield return Option_Postpone;
            }
        }

        /// <summary>Unanswered when the time runs out: the default happens.</summary>
        public override void Removed()
        {
            base.Removed();
            if (!resolved) WorldSituations.Instance?.Resolve(this, Beat?.defaultOption ?? 0, true);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref ctx, "ctx");
            Scribe_Values.Look(ref beatKey, "beatKey");
            Scribe_Values.Look(ref resolved, "resolved");
        }
    }
}
