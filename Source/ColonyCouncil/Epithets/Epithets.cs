using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A name the colony gives someone for what they keep doing: the Healer, the Deadeye, the Kindling.
    /// Goes to whoever has the highest count of a record, once it's past a threshold, and moves when someone
    /// overtakes them.
    /// </summary>
    public class EpithetDef : Def
    {
        public RecordDef record;
        public float threshold = 10f;
        /// <summary>How far ahead a challenger must be to take the name (so it doesn't flip back and forth).</summary>
        public float margin = 1.15f;
        /// <summary>Earning this one is a mixed blessing (the Kindling): no pride, just a name.</summary>
        public bool dubious;
        [MustTranslate] public string earned;
    }

    public class EpithetTracker : IExposable
    {
        private Dictionary<string, Pawn> holders = new Dictionary<string, Pawn>();

        public Pawn HolderOf(EpithetDef def) => holders.TryGetValue(def.defName, out var p) ? p : null;

        public IEnumerable<EpithetDef> Of(Pawn p) => DefDatabase<EpithetDef>.AllDefsListForReading.Where(d => HolderOf(d) == p);

        public string TitlesOf(Pawn p) => string.Join(", ", Of(p).Select(d => d.label));

        private bool initialized;

        public void Daily(CouncilGameComponent comp)
        {
            if (!ColonyCouncilMod.Settings.epithets) return;
            var pawns = PawnsFinder.AllMaps_FreeColonists.Where(p => p.records != null && !p.IsQuestLodger()).ToList();
            bool quiet = !initialized;
            initialized = true;
            int awarded = 0;
            var firstRound = new List<string>();
            foreach (var def in DefDatabase<EpithetDef>.AllDefsListForReading.InRandomOrder())
            {
                // A couple of new names a day at most; the rest can wait for tomorrow.
                if (!quiet && awarded >= 2) break;
                if (def.record == null) continue;
                var holder = HolderOf(def);
                if (holder != null && (holder.Dead || !holder.IsColonist))
                {
                    holders.Remove(def.defName);
                    holder = null;
                }
                var best = pawns.OrderByDescending(p => p.records.GetValue(def.record)).FirstOrDefault();
                if (best == null || best == holder) continue;
                float value = best.records.GetValue(def.record);
                if (value < def.threshold) continue;
                if (holder != null && value < holder.records.GetValue(def.record) * def.margin) continue;
                if (quiet)
                {
                    // The colony already had names for people before the council started keeping track.
                    holders[def.defName] = best;
                    firstRound.Add(best.LabelShort + " " + def.label);
                    continue;
                }
                Award(def, best, holder, comp);
                awarded++;
            }
            if (firstRound.Count > 0)
                Messages.Message("CC_Epithet_FirstRound".Translate(TextUtil.JoinAnd(firstRound)).Resolve(), MessageTypeDefOf.NeutralEvent, false);
        }

        private void Award(EpithetDef def, Pawn p, Pawn previous, CouncilGameComponent comp)
        {
            holders[def.defName] = p;
            string text = def.earned.Formatted(p.Named("PAWN")).Resolve();
            if (previous != null) text += " " + "CC_Epithet_Taken".Translate(previous.Named("OTHER"), def.label.Named("TITLE")).Resolve();
            Messages.Message(text, new LookTargets(p), def.dubious ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.PositiveEvent, false);
            CouncilGameComponent.GiveThought(p, def.dubious ? CouncilDefOf.Council_EpithetDubious : CouncilDefOf.Council_Epithet);
            if (previous != null && !def.dubious) CouncilGameComponent.GiveThought(previous, CouncilDefOf.Council_EpithetLost, p);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Chronicle_Epithet".Translate(p.LabelShort, def.label).Resolve(), p));
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref holders, "holders", LookMode.Value, LookMode.Reference, ref keys, ref values);
            Scribe_Values.Look(ref initialized, "initialized");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (holders == null) holders = new Dictionary<string, Pawn>();
                foreach (var k in holders.Where(kv => kv.Value == null || DefDatabase<EpithetDef>.GetNamedSilentFail(kv.Key) == null).Select(kv => kv.Key).ToList())
                    holders.Remove(k);
            }
        }

        private List<string> keys;
        private List<Pawn> values;
    }
}
