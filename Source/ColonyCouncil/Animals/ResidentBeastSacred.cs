using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The question about a beast that stays taken for a god's: {GOD} is the believer's own name for the god they take it for (a belief, never the truth).</summary>
    public class DilemmaContext_SacredBeast : DilemmaContext
    {
        public string god;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            args.Add((god ?? "").Named("GOD"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref god, "god");
        }
    }

    /// <summary>
    /// The beast that stays, taken for a god's when it was named (Gods/OnMap/GodBeasts.cs; docs/content-update-proposals.md,
    /// C4): the council's question with the believer's own answer (Council_DlmA_ResidentBeastSacred). X the beast, A the
    /// believer who took it for the god's (grown and at home), else the one who named it or whoever is best with animals, B
    /// the best shot.
    /// </summary>
    public class DilemmaWorker_ResidentBeastSacred : DilemmaWorker
    {
        private static ResidentBeast Beast(Map map) => CouncilGameComponent.Instance?.BeastOf(map);

        private static bool Here(Pawn p, Map map) => p != null && !p.Dead && p.Spawned && p.Map == map && p.IsColonist && !p.DevelopmentalStage.Juvenile();

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var rb = Beast(map);
            var taken = rb == null || rb.takenGod < 0 ? null : Gods.ById(rb.takenGod);
            if (rb == null || taken == null || !rb.Here || rb.animal.Map != map || !CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p) && !p.DevelopmentalStage.Juvenile()).ToList();
            if (pawns.Count < def.minColonists) return false;
            var a = Here(rb.taker, map) ? rb.taker : Here(rb.namer, map) ? rb.namer : VarietyUtil.Best(map, SkillDefOf.Animals);
            if (a == null) return false;
            var b = pawns.Where(p => p != a).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).FirstOrDefault();
            ctx = new DilemmaContext_SacredBeast { def = def, map = map, a = a, b = b, stranger = rb.animal, god = PawnGods.NameOf(taken, a) };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var rb = Beast(ctx?.map);
            if (rb == null) return;
            foreach (var t in rb.Tags()) tags.Add(t);
            if (rb.asks >= 1) tags.Add("asked" + Mathf.Min(3, rb.asks + 1));
            // The believer is the one who named it.
            if (rb.taker != null && rb.taker == rb.namer) tags.Add("namer");
        }
    }

    /// <summary>
    /// The colony leaves the beast be as its believer asks: the book keeps it under the god they took it for, and the god it
    /// is like is glad of it (hidden, as everything the gods are: nothing of it is shown).
    /// </summary>
    public class DilemmaEffect_SparedForGod : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Apply(DilemmaContext ctx)
        {
            var rb = CouncilGameComponent.Instance?.BeastOf(ctx?.map);
            if (rb?.animal == null) return;
            GodBeasts.Spared(rb, ctx.map);
        }
    }
}
