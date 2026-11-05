using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The council's questions about fealty (docs/design/stage2-diplomacy.md, item 9: "the council hears it, with those who
    /// object"; "renouncing is a council question"). When their lord stands at the gate to take the colony's oath, a
    /// colonist who objects has their say before the council first (Rimfolk_DlmFealty_Swear): one sworn to vengeance on
    /// them, a proud colonist who won't bend the knee, or a devout colonist of another faith than theirs. Sworn all the
    /// same, the objector takes it hard; sent home, their people feel turned away. Renouncing the oath is asked of the
    /// council with its cost named (Rimfolk_DlmFealty_Renounce): their grudge, or nothing after a season they failed to
    /// protect the colony. Asked when no other question of the council's is open at the home, else the hour after.
    /// </summary>
    public static class FealtyObjection
    {
        /// <summary>The people and lord the council is being asked about, while the question is cast.</summary>
        internal static Faction asking;
        internal static Pawn lord;
        internal static Tie renouncing;

        /// <summary>Faith holds this firm (Ideology's certainty) for a colonist of another faith to object.</summary>
        public const float DevoutAt = 0.8f;

        /// <summary>The one who objects to swearing to them at this home, and why; null with None for nobody.</summary>
        public static Pawn Objector(Faction f, Map home, out FealtyObjector why)
        {
            why = FealtyObjector.None;
            if (f == null || home == null) return null;
            var v = TruceObjection.Avenger(f, home);
            if (v != null)
            {
                why = FealtyObjector.Avenger;
                return v.avenger;
            }
            var theirFaith = ModsConfig.IdeologyActive ? f.ideos?.PrimaryIdeo : null;
            Pawn best = null;
            foreach (var p in IncidentWorker_Happening.Colonists(home))
            {
                if (p == null || p.DevelopmentalStage.Juvenile() || !p.Spawned) continue;
                bool devout = theirFaith != null && p.Ideo != null && p.Ideo != theirFaith && p.ideo.Certainty >= DevoutAt;
                var w = FealtyMath.Objector(false, Persona.Get(p, PersonaAxis.Pride), devout);
                if (w == FealtyObjector.None) continue;
                // The proud before the devout, and of those the proudest.
                if (best == null || w < why || w == why && Persona.Get(p, PersonaAxis.Pride) > Persona.Get(best, PersonaAxis.Pride))
                {
                    best = p;
                    why = w;
                }
            }
            return best;
        }

        /// <summary>The council's question about swearing to them is still open in the letters.</summary>
        public static bool SwearOpen(Faction f)
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_Dilemma d && !d.resolved && d.ctx is FealtyContext c && !c.renounce && c.faction == f)
                    return true;
            return false;
        }

        /// <summary>The council's question about renouncing this oath is still open in the letters.</summary>
        public static bool RenounceOpen(int tie)
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_Dilemma d && !d.resolved && d.ctx is FealtyContext c && c.renounce && c.tie == tie)
                    return true;
            return false;
        }

        /// <summary>Another question of the council's for this home still open in the letters.</summary>
        private static bool CouncilBusy(Map home)
        {
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_Dilemma d && !d.resolved && (d.ctx?.map == null || d.ctx.map == home || !Homes.IsHome(d.ctx.map)))
                    return true;
            return false;
        }

        /// <summary>
        /// The objection comes before the council now: true when it was asked, or waits its turn (another question is open
        /// first), so the speaker waits for the answer. False when nobody objects.
        /// </summary>
        public static bool AskSwear(Faction f, Pawn x, Map home)
        {
            if (Objector(f, home, out _) == null) return false;
            if (CouncilBusy(home)) return true;
            asking = f;
            lord = x;
            try
            {
                var tracker = DilemmaTracker.For(home) ?? CouncilGameComponent.Instance?.dilemmas;
                return tracker != null && tracker.TryFire(true, d => d.Worker is DilemmaWorker_FealtySwear);
            }
            finally
            {
                asking = null;
                lord = null;
            }
        }

        /// <summary>Renouncing asked of the council (the act on the tie's or the people's card): its question now, or a word that another is open first.</summary>
        public static bool AskRenounce(Tie t)
        {
            var home = Fealty.HomeOf(t);
            if (Fealty.RenounceWhyNot(t) != null || home == null) return false;
            if (CouncilBusy(home))
            {
                Messages.Message(FealtyWords.P("CC_Fealty_Why_CouncilBusy"), MessageTypeDefOf.RejectInput, false);
                return false;
            }
            renouncing = t;
            try
            {
                var tracker = DilemmaTracker.For(home) ?? CouncilGameComponent.Instance?.dilemmas;
                return tracker != null && tracker.TryFire(true, d => d.Worker is DilemmaWorker_FealtyRenounce);
            }
            finally
            {
                renouncing = null;
            }
        }
    }

    /// <summary>The questions' context: A the objector (or the speaker), X their lord at the gate, {FACTION} the liege, {WORD} the oath in the colony's age.</summary>
    public class FealtyContext : DilemmaContext
    {
        public bool renounce;
        public int tie = -1;
        public string word;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((word ?? "").Named("WORD"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref renounce, "renounce");
            Scribe_Values.Look(ref tie, "tie", -1);
            Scribe_Values.Look(ref word, "word");
        }
    }

    /// <summary>Rimfolk_DlmFealty_Swear: only from FealtyObjection.AskSwear, with their lord at the gate. Tags, why A objects: "avenger", "proud", "faith"; "pet" for a vengeance sworn for an animal.</summary>
    public class DilemmaWorker_FealtySwear : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var f = FealtyObjection.asking;
            if (f == null) return false;
            var a = FealtyObjection.Objector(f, map, out _);
            if (a == null) return false;
            ctx = new FealtyContext { def = def, map = map, a = a, faction = f, stranger = FealtyObjection.lord, word = FealtyWords.Form(map, f).word };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.faction == null) return;
            FealtyObjection.Objector(ctx.faction, ctx.map, out var why);
            if (why != FealtyObjector.None) tags.Add(why.ToString().ToLowerInvariant());
            var v = TruceObjection.Avenger(ctx.faction, ctx.map);
            if (why == FealtyObjector.Avenger && v != null && v.pet) tags.Add("pet");
        }
    }

    /// <summary>The oath is sworn all the same: the speaker walks out to their lord.</summary>
    public class DilemmaEffect_FealtySwear : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.faction == null) return;
            sheet.Play(FealtyWords.P("CC_Impact_FealtySwear", FealtyWords.People(ctx.faction).Named("PEOPLE")), FealtyWords.P("CC_Impact_FealtySwearBrief"));
            sheet.Add(ImpactKind.Goods, -1, FealtyWords.P("CC_Impact_FealtyTribute", Fealty.TributeValue(ctx.map).Named("TRIBUTE")), FealtyWords.P("CC_Impact_FealtyTributeBrief"));
            sheet.Add(ImpactKind.Health, -1, FealtyWords.P("CC_Impact_FealtyLevies"), FealtyWords.P("CC_Impact_FealtyLeviesBrief"));
        }

        public override string Unavailable(DilemmaContext ctx) => FealtyOath.LordOf(ctx?.map) == null ? FealtyWords.P("CC_Fealty_Why_LordGone") : null;

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's answer; the speaker walks out to their lord in the hour that follows.
            FealtyOath.Allow(ctx?.map);
        }
    }

    /// <summary>Their lord is sent home: the oath lapses, and their people feel turned away.</summary>
    public class DilemmaEffect_FealtyTurnAway : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.faction == null) return;
            sheet.Add(ImpactKind.People, -1, FealtyWords.P("CC_Impact_FealtyTurnAway", FealtyWords.People(ctx.faction).Named("PEOPLE")), FealtyWords.P("CC_Impact_FealtyTurnAwayBrief"));
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's answer; their lord turns from the gate and walks home.
            if (ctx?.faction != null) FealtyOath.TurnAway(ctx.map, ctx.faction);
        }
    }

    /// <summary>Rimfolk_DlmFealty_Renounce: only from FealtyObjection.AskRenounce. A the speaker; tags "clean" (they failed to protect the colony this season), "owed" (tribute still owed).</summary>
    public class DilemmaWorker_FealtyRenounce : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var t = FealtyObjection.renouncing;
            var f = Fealty.LiegeOf(t);
            if (t == null || f == null) return false;
            var a = TruceParley.Speaker(map) ?? IncidentWorker_Happening.Colonists(map).FirstOrDefault(p => !p.DevelopmentalStage.Juvenile());
            if (a == null) return false;
            ctx = new FealtyContext { def = def, map = map, a = a, faction = f, renounce = true, tie = t.id, word = FealtyWords.Form(map, f).word };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var t = ctx is FealtyContext c ? Fealty.Get(c.tie) : null;
            if (t == null) return;
            if (Fealty.FailedSeason(t)) tags.Add("clean");
            if (Fealty.Owed(t)) tags.Add("owed");
        }
    }

    /// <summary>The oath renounced: ended, at the cost the question named.</summary>
    public class DilemmaEffect_FealtyRenounce : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var t = ctx is FealtyContext c ? Fealty.Get(c.tie) : null;
            if (t == null || ctx.faction == null) return;
            int cost = FealtyMath.RenounceCost(Fealty.FailedSeason(t));
            if (cost > 0)
                sheet.Add(ImpactKind.Goodwill, -1, FealtyWords.P("CC_Impact_FealtyRenounce", FealtyWords.People(ctx.faction).Named("PEOPLE"), cost.Named("GOODWILL")),
                    FealtyWords.P("CC_Impact_FealtyRenounceBrief", cost.Named("GOODWILL")));
            else sheet.Add(ImpactKind.People, 0, FealtyWords.P("CC_Impact_FealtyRenounceClean", FealtyWords.People(ctx.faction).Named("PEOPLE")), FealtyWords.P("CC_Impact_FealtyRenounceCleanBrief"));
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            var t = ctx is FealtyContext c ? Fealty.Get(c.tie) : null;
            return t == null || !t.Live ? FealtyWords.P("CC_Fealty_Why_Gone") : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's answer, sent to their people by word.
            if (ctx is FealtyContext c) Fealty.Renounce(Fealty.Get(c.tie));
        }
    }
}
