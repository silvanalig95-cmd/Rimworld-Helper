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
        /// <summary>
        /// Only for someone with a place in the colony's years: "founder" (here since its first days), "lastFounder" (the
        /// only founder still here, of two or more), "firstborn" (the first child born here, now grown).
        /// </summary>
        public string only;
        /// <summary>A line for their death letter while they hold the name ({PAWN_...}): what the colony loses with them.</summary>
        [MustTranslate] public string deathNote;

        /// <summary>Whether this colonist can hold the name at all (the record conditions aside from the count).</summary>
        public bool Allows(Pawn p)
        {
            if (!only.NullOrEmpty() && !(CouncilGameComponent.Instance?.epithets?.Fits(only, p) ?? false)) return false;
            if (requireNone.NullOrEmpty()) return true;
            foreach (var r in requireNone)
                if (r != null && p.records.GetValue(r) > 0f) return false;
            return true;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!only.NullOrEmpty() && !EpithetTracker.Conditions.Contains(only)) yield return "unknown condition " + only;
        }
    }

    public class EpithetTracker : IExposable
    {
        private Dictionary<string, Pawn> holders = new Dictionary<string, Pawn>();
        /// <summary>Everyone who was here in the colony's first days (ThingIDs), the living and the gone.</summary>
        private List<string> founders = new List<string>();
        /// <summary>The first child born into the colony (ThingID), once one has been.</summary>
        private string firstbornId;

        /// <summary>The conditions a name can ask for (<see cref="EpithetDef.only"/>).</summary>
        public static readonly HashSet<string> Conditions = new HashSet<string> { "founder", "lastFounder", "firstborn" };

        public bool IsFounder(Pawn p) => p != null && founders.Contains(p.ThingID);

        /// <summary>Whether someone has the place in the colony's years a name asks for.</summary>
        public bool Fits(string only, Pawn p)
        {
            if (p == null) return false;
            switch (only)
            {
                case "founder": return IsFounder(p);
                case "lastFounder":
                {
                    if (!IsFounder(p)) return false;
                    int here = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Count(IsFounder);
                    return ColonyTraitMath.LastOfTheFirst(founders.Count, here, GenDate.DaysPassed / (float)GenDate.DaysPerYear);
                }
                case "firstborn": return firstbornId != null && p.ThingID == firstbornId && !p.DevelopmentalStage.Juvenile();
                default: return false;
            }
        }

        /// <summary>
        /// Writes down the colony's founders (those who joined in its first days, by their time as colonists, so an older
        /// save finds its own) and the first child born here (the calendar's first child, or the eldest born here).
        /// </summary>
        public void NoteFounders(List<Pawn> colonists)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var p in colonists)
            {
                if (p?.records == null || founders.Contains(p.ThingID)) continue;
                int here = (int)p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal);
                if (here > 0 && ColonyTraitMath.Founder(now - here, GenDate.TicksPerDay)) founders.Add(p.ThingID);
            }
            if (firstbornId != null || !ModsConfig.BiotechActive) return;
            string fromCalendar = CalendarTracker.Instance?.Get("firstchild")?.pawnId;
            if (!fromCalendar.NullOrEmpty())
            {
                firstbornId = fromCalendar;
                return;
            }
            var born = colonists.Where(p => p.ageTracker != null && p.records != null
                                            && p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) >= p.ageTracker.AgeBiologicalTicks - GenDate.TicksPerDay * 2)
                .OrderByDescending(p => p.ageTracker.AgeBiologicalTicks).FirstOrDefault();
            if (born != null) firstbornId = born.ThingID;
        }

        public Pawn HolderOf(EpithetDef def) => def != null && holders.TryGetValue(def.defName, out var p) ? p : null;

        /// <summary>Who holds the name of this defName (alive and here), or null.</summary>
        public Pawn HolderOf(string defName) =>
            defName != null && holders.TryGetValue(defName, out var p) && p != null && !p.Dead && p.IsColonist ? p : null;

        public IEnumerable<EpithetDef> Of(Pawn p) => DefDatabase<EpithetDef>.AllDefsListForReading.Where(d => HolderOf(d) == p);

        public string TitlesOf(Pawn p) => string.Join(", ", Of(p).Select(d => d.label));

        private bool initialized;

        public void Daily(CouncilGameComponent comp)
        {
            foreach (var k in holders.Where(kv => kv.Value == null || kv.Value.Dead || !kv.Value.IsColonist).Select(kv => kv.Key).ToList())
                holders.Remove(k);
            if (!ColonyCouncilMod.Settings.epithets) return;
            var pawns = PawnsFinder.AllMaps_FreeColonists.Where(p => p.records != null && !p.IsQuestLodger()).ToList();
            // A day more in the seats and roles the names count, and who founded the colony.
            try
            {
                NameRecords.Daily(this, pawns);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("names: records", e);
            }
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
                if (value < def.threshold * Reputations.EpithetEase(def, best)) continue;
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
            Scribe_Collections.Look(ref founders, "founders", LookMode.Value);
            Scribe_Values.Look(ref firstbornId, "firstbornId");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (founders == null) founders = new List<string>();
                if (holders == null) holders = new Dictionary<string, Pawn>();
                foreach (var k in holders.Where(kv => kv.Value == null || DefDatabase<EpithetDef>.GetNamedSilentFail(kv.Key) == null).Select(kv => kv.Key).ToList())
                    holders.Remove(k);
            }
        }

        private List<string> keys;
        private List<Pawn> values;
    }
}
