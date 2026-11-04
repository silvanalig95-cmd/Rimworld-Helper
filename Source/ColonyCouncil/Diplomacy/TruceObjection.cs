using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A truce with a people one of the colony swore vengeance on (docs/design/stage2-diplomacy.md, item 2): when their
    /// envoy stands at the gate, the avenger has their say before the council first (Rimfolk_DlmTruce_Avenger, as a guest
    /// to be taken in comes with the one who objects). Sworn all the same, the avenger takes it as peace with the killers;
    /// sent away, their envoy goes home and their people feel turned away.
    /// </summary>
    public static class TruceObjection
    {
        /// <summary>The truce the council is being asked about, while the question is cast.</summary>
        internal static Tie asking;
        internal static Pawn asker, envoy;

        /// <summary>One of the colony at this home, grown, sworn to vengeance on them and not yet avenged; null for none.</summary>
        public static Vendetta Avenger(Faction f, Map home)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp?.vendettas?.vendettas == null || f == null) return null;
            return comp.vendettas.vendettas.FirstOrDefault(v => v != null && v.Active && v.faction == f && v.avenger.Spawned && v.avenger.Map == home
                                                                && !v.avenger.DevelopmentalStage.Juvenile() && KinMemory.Ours(v.avenger));
        }

        /// <summary>The council's question about this truce is still open in the letters.</summary>
        public static bool Open(int tie)
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_Dilemma d && !d.resolved && d.ctx is TruceContext c && c.tie == tie)
                    return true;
            return false;
        }

        /// <summary>The avenger's objection comes before the council now: true when it was asked (the speaker waits for the answer).</summary>
        public static bool Ask(Tie t, Pawn x, Map home)
        {
            var f = Truces.PeopleOf(t);
            var v = Avenger(f, home);
            if (v == null) return false;
            asking = t;
            asker = v.avenger;
            envoy = x;
            try
            {
                var tracker = DilemmaTracker.For(home) ?? CouncilGameComponent.Instance?.dilemmas;
                return tracker != null && tracker.TryFire(true, d => d.Worker is DilemmaWorker_TruceAvenger);
            }
            finally
            {
                asking = null;
                asker = null;
                envoy = null;
            }
        }
    }

    /// <summary>The objection's question: A the avenger, X their envoy, {VICTIM} the one the vengeance is for.</summary>
    public class TruceContext : DilemmaContext
    {
        public int tie = -1;
        public string victim;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((victim ?? "").Named("VICTIM"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tie, "tie", -1);
            Scribe_Values.Look(ref victim, "victim");
        }
    }

    /// <summary>Rimfolk_DlmTruce_Avenger: only from TruceObjection.Ask, with their envoy at the gate. Tags: "pet" for a vendetta sworn for an animal.</summary>
    public class DilemmaWorker_TruceAvenger : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var t = TruceObjection.asking;
            var a = TruceObjection.asker;
            if (t == null || a == null) return false;
            var f = Truces.PeopleOf(t);
            var v = TruceObjection.Avenger(f, a.Map);
            ctx = new TruceContext { def = def, map = a.Map, a = a, faction = f, stranger = TruceObjection.envoy, tie = t.id, victim = v?.victim ?? "" };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var v = TruceObjection.Avenger(ctx?.faction, ctx?.map);
            if (v != null && v.pet) tags.Add("pet");
        }
    }

    /// <summary>The truce is sworn all the same: the speaker walks out to their envoy.</summary>
    public class DilemmaEffect_TruceSwear : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is TruceContext c) || c.faction == null) return;
            sheet.Play(PactWords.P("CC_Impact_TruceSwear", PactWords.People(c.faction).Named("PEOPLE")), PactWords.P("CC_Impact_TruceSwearBrief"));
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var t = ctx is TruceContext c ? Truces.Get(c.tie) : null;
            return t == null || !Truces.Pending(t) ? PactWords.P("CC_Truce_Why_Gone") : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var t = ctx is TruceContext c ? Truces.Get(c.tie) : null;
            if (t == null || !Truces.Pending(t)) return;
            // apart: the council's answer; the speaker walks out to the envoy in the hour that follows.
            TruceParley.Allow(t.id);
        }
    }

    /// <summary>Their envoy is sent home: the truce lapses, and their people feel turned away.</summary>
    public class DilemmaEffect_TruceTurnAway : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is TruceContext c) || c.faction == null) return;
            sheet.Add(ImpactKind.People, -1, PactWords.P("CC_Impact_TruceTurnAway", PactWords.People(c.faction).Named("PEOPLE")), PactWords.P("CC_Impact_TruceTurnAwayBrief"));
        }

        public override void Apply(DilemmaContext ctx)
        {
            var t = ctx is TruceContext c ? Truces.Get(c.tie) : null;
            // apart: the council's answer; their envoy turns from the gate and walks home.
            if (t != null && Truces.Pending(t)) TruceParley.TurnAway(t);
        }
    }
}
