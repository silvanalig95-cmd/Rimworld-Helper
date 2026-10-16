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
        /// <summary>
        /// Records that must still be at nothing (the Unshaken: never a mental break). Someone who gets one loses the
        /// name, and can't earn it again.
        /// </summary>
        public List<RecordDef> requireNone;

        /// <summary>Whether this colonist can hold the name at all (the record conditions aside from the count).</summary>
        public bool Allows(Pawn p)
        {
            if (requireNone.NullOrEmpty()) return true;
            foreach (var r in requireNone)
                if (r != null && p.records.GetValue(r) > 0f) return false;
            return true;
        }
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
            foreach (var k in holders.Where(kv => kv.Value == null || kv.Value.Dead || !kv.Value.IsColonist).Select(kv => kv.Key).ToList())
                holders.Remove(k);
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
                // A name that depends on never doing something (never breaking) is lost the day they do.
                if (holder != null && holder.records != null && !def.Allows(holder))
                {
                    holders.Remove(def.defName);
                    if (!quiet) News.Post("CC_Epithet_NoLonger".Translate(holder.Named("PAWN"), def.label.Named("TITLE")).Resolve(), new LookTargets(holder),
                        NewsKind.People, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
                    holder = null;
                }
                var best = pawns.Where(def.Allows).OrderByDescending(p => p.records.GetValue(def.record)).FirstOrDefault();
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
                News.Post("CC_Epithet_FirstRound".Translate(TextUtil.JoinAnd(firstRound)).Resolve(), null, NewsKind.People, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// A name earned some other way than by a record (a personal story's ending): given only if nobody living here
        /// holds it already. Names without a record stay with their holder.
        /// </summary>
        public bool Bestow(EpithetDef def, Pawn p, CouncilGameComponent comp)
        {
            if (def == null || p == null || comp == null || !ColonyCouncilMod.Settings.epithets) return false;
            var holder = HolderOf(def);
            if (holder != null && !holder.Dead && holder.IsColonist) return false;
            Award(def, p, null, comp);
            return true;
        }

        private void Award(EpithetDef def, Pawn p, Pawn previous, CouncilGameComponent comp)
        {
            using var _cause = Causes.Push("CC_Cause_Epithet".Translate(p.LabelShort, def.label).Resolve());
            // apart: news of a name the colony gave someone; the one who lost it minds.
            holders[def.defName] = p;
            string text = def.earned.Formatted(p.Named("PAWN")).Resolve();
            if (previous != null) text += " " + "CC_Epithet_Taken".Translate(previous.Named("OTHER"), def.label.Named("TITLE")).Resolve();
            News.Post(text, new LookTargets(p), NewsKind.People, def.dubious ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.PositiveEvent, Importance.Notable);
            CouncilGameComponent.GiveThought(p, def.dubious ? CouncilDefOf.Council_EpithetDubious : CouncilDefOf.Council_Epithet);
            if (previous != null && !def.dubious)
            {
                CouncilGameComponent.GiveThought(previous, CouncilDefOf.Council_EpithetLost, p);
                comp.social.Bump(previous, p, TieKind.Rivals, 12f, "CC_TieNote_Name".Translate(def.label));
            }
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Chronicle_Epithet".Translate(p.LabelShort, def.label).Resolve(), p));
            comp.journals.Write(p, def.dubious ? "Council_Journal_EpithetDubious" : "Council_Journal_Epithet", null, def.label.Named("TITLE"));
            Setbacks.Notify_Named(p, def);
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
