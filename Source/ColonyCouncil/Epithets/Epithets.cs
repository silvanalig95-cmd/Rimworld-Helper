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

    /// <summary>
    /// The names the colony gives its people, and its founders and firstborn. Each colony gives its own (stage 0, step 7b):
    /// a name is kept under the colony it was given in (<see cref="EpithetMath.Key"/>; the name alone with one colony, as
    /// always), "the Healer" is the best healer among that colony's people, and taking a name over makes a rival only
    /// within one colony. The game's first founders and firstborn are the colony's settled with the game (else the seat's);
    /// a colony settled later has its own, those who settled it and the first child born there.
    /// </summary>
    public class EpithetTracker : IExposable
    {
        private Dictionary<string, Pawn> holders = new Dictionary<string, Pawn>();
        /// <summary>Everyone who was here in the colony's first days (ThingIDs), the living and the gone: the game's first colony's.</summary>
        private List<string> founders = new List<string>();
        /// <summary>The first child born into the colony (ThingID), once one has been: the game's first colony's.</summary>
        private string firstbornId;
        /// <summary>A colony settled later: those who settled it, and the first child born there ("colony|ThingID", EpithetMath.Entry).</summary>
        private List<string> laterFounders = new List<string>();
        private List<string> laterFirstborn = new List<string>();

        /// <summary>The conditions a name can ask for (<see cref="EpithetDef.only"/>).</summary>
        public static readonly HashSet<string> Conditions = new HashSet<string> { "founder", "lastFounder", "firstborn" };

        // ---------------- Which colony ----------------

        /// <summary>More than one colony: names are kept per colony.</summary>
        private static bool Several => ColonyState.Several;

        /// <summary>The colony someone belongs to (its key), or none without a home.</summary>
        private static int ColonyOf(Pawn p) => p == null ? HomesMath.None : Homes.KeyOf(Homes.Of(p));

        /// <summary>The settlement of this home was made with the game (its first days), as the calendar counts a founding.</summary>
        private static bool SettledWithGame(Map m)
        {
            int settled = m?.Parent != null && m.Parent.creationGameTicks >= 0 ? m.Parent.creationGameTicks : -1;
            return CalendarMath.FoundedTick(settled, Find.TickManager.TicksGame) == 0;
        }

        /// <summary>The colony whose founders and firstborn are the game's (EpithetMath.FirstColony).</summary>
        private static int FirstColony()
        {
            var homes = Homes.All;
            return EpithetMath.FirstColony(homes.Select(Homes.KeyOf).ToList(), homes.Select(SettledWithGame).ToList(), Homes.KeyOf(Homes.Seat));
        }

        /// <summary>When a home's settlement was made (a game tick), or -1 when the game doesn't say.</summary>
        private static int Settled(Map m) => m?.Parent != null && m.Parent.creationGameTicks >= 0 ? m.Parent.creationGameTicks : -1;

        // ---------------- Founders and firstborn ----------------

        /// <summary>One of the founders of their own colony.</summary>
        public bool IsFounder(Pawn p)
        {
            if (p == null) return false;
            if (!Several) return founders.Contains(p.ThingID);
            int colony = ColonyOf(p);
            return colony == FirstColony() ? founders.Contains(p.ThingID) : laterFounders.Contains(EpithetMath.Entry(colony, p.ThingID));
        }

        /// <summary>Whether someone has the place in their colony's years a name asks for.</summary>
        public bool Fits(string only, Pawn p)
        {
            if (p == null) return false;
            switch (only)
            {
                case "founder": return IsFounder(p);
                case "lastFounder":
                {
                    if (!IsFounder(p)) return false;
                    if (!Several)
                    {
                        int here = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Count(IsFounder);
                        return ColonyTraitMath.LastOfTheFirst(founders.Count, here, GenDate.DaysPassed / (float)GenDate.DaysPerYear);
                    }
                    // The founders of their own colony, ever and still here, and the years since it was settled.
                    int colony = ColonyOf(p);
                    bool first = colony == FirstColony();
                    var map = Homes.MapOf(colony);
                    int ever = first ? founders.Count : laterFounders.Count(e => e.StartsWith(colony + EpithetMath.Sep.ToString()));
                    int still = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Count(o => ColonyOf(o) == colony && IsFounder(o));
                    float years = (Find.TickManager.TicksGame - (first ? 0 : System.Math.Max(0, Settled(map)))) / (float)GenDate.TicksPerYear;
                    return ColonyTraitMath.LastOfTheFirst(ever, still, years);
                }
                case "firstborn":
                {
                    if (p.DevelopmentalStage.Juvenile()) return false;
                    if (!Several) return firstbornId != null && p.ThingID == firstbornId;
                    int colony = ColonyOf(p);
                    return colony == FirstColony() ? firstbornId != null && p.ThingID == firstbornId : laterFirstborn.Contains(EpithetMath.Entry(colony, p.ThingID));
                }
                default: return false;
            }
        }

        /// <summary>
        /// Writes down the colony's founders (those who joined in its first days, by their time as colonists, so an older
        /// save finds its own) and the first child born here (the calendar's first child, or the eldest born here). A
        /// colony settled later has those who were its people in its own first days, and its own calendar's first child.
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
            if (Several) NoteLaterFounders(colonists, now);
            if (firstbornId != null || !ModsConfig.BiotechActive) return;
            // The first child on the calendar of the colony of the game's first days.
            string fromCalendar = ColonyCalendar.For(Homes.MapOf(FirstColony()))?.Get("firstchild")?.pawnId;
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

        /// <summary>The colonies settled later: those who are theirs in their first days, and the first child on their calendar.</summary>
        private void NoteLaterFounders(List<Pawn> colonists, int now)
        {
            int first = FirstColony();
            foreach (var home in Homes.All)
            {
                int key = Homes.KeyOf(home);
                if (key == first) continue;
                if (EpithetMath.FirstDays(now, Settled(home), GenDate.TicksPerDay))
                    foreach (var p in colonists)
                    {
                        if (p == null || ColonyOf(p) != key) continue;
                        string e = EpithetMath.Entry(key, p.ThingID);
                        if (!laterFounders.Contains(e)) laterFounders.Add(e);
                    }
                string prefix = key + EpithetMath.Sep.ToString();
                if (!ModsConfig.BiotechActive || laterFirstborn.Any(e => e.StartsWith(prefix))) continue;
                string child = ColonyCalendar.For(home)?.Get("firstchild")?.pawnId;
                if (!child.NullOrEmpty()) laterFirstborn.Add(EpithetMath.Entry(key, child));
            }
            // Kept in bounds: entries of colonies no longer ours go.
            laterFounders.RemoveAll(e => !StillHome(e));
            laterFirstborn.RemoveAll(e => !StillHome(e));
        }

        private static bool StillHome(string entry)
        {
            int i = entry?.IndexOf(EpithetMath.Sep) ?? -1;
            return i > 0 && int.TryParse(entry.Substring(0, i), out int key) && Homes.MapOf(key) != null;
        }

        // ---------------- Who holds a name ----------------

        /// <summary>Who holds a name in a colony (with one colony, its holder, as always).</summary>
        private Pawn Lookup(string defName, int colony)
        {
            if (defName == null) return null;
            if (!Several) return holders.TryGetValue(defName, out var one) ? one : holders.TryGetValue(EpithetMath.Key(defName, colony, true), out var kept) ? kept : null;
            if (holders.TryGetValue(EpithetMath.Key(defName, colony, true), out var p)) return p;
            // A name given while there was one colony: its holder's colony's.
            return holders.TryGetValue(defName, out var old) && old != null && ColonyOf(old) == colony ? old : null;
        }

        /// <summary>The name goes to someone in a colony (with one colony, kept under the name alone, as always).</summary>
        private void Write(string defName, int colony, Pawn p)
        {
            Remove(defName, colony);
            holders[EpithetMath.Key(defName, colony, Several)] = p;
        }

        /// <summary>A colony's holder of a name is let go.</summary>
        private void Remove(string defName, int colony)
        {
            if (holders.TryGetValue(defName, out var old) && (!Several || old == null || ColonyOf(old) == colony)) holders.Remove(defName);
            holders.Remove(EpithetMath.Key(defName, colony, true));
        }

        /// <summary>Who holds this name in the colony of this map (the seat's for a map that isn't a home).</summary>
        public Pawn HolderOf(EpithetDef def, Map map) => def == null ? null : Lookup(def.defName, Homes.KeyOf(Homes.IsHome(map) ? map : Homes.Seat));

        /// <summary>Who holds the name of this defName in the colony of this map (alive and here), or null.</summary>
        public Pawn HolderOf(string defName, Map map)
        {
            var p = Lookup(defName, Homes.KeyOf(Homes.IsHome(map) ? map : Homes.Seat));
            return p != null && !p.Dead && p.IsColonist ? p : null;
        }

        /// <summary>Who holds the name of this defName (alive and here): the seat's, else any colony's. For the colony as a whole.</summary>
        public Pawn HolderOf(string defName)
        {
            if (defName == null) return null;
            var p = HolderOf(defName, Homes.Seat);
            if (p != null || !Several) return p;
            foreach (var kv in holders)
                if (EpithetMath.DefOf(kv.Key, out _) == defName && kv.Value != null && !kv.Value.Dead && kv.Value.IsColonist) return kv.Value;
            return null;
        }

        /// <summary>Someone holds this name, in whichever colony.</summary>
        private bool Holds(Pawn p, EpithetDef d)
        {
            if (p == null || d == null) return false;
            if (!Several) return holders.TryGetValue(d.defName, out var one) && one == p;
            foreach (var kv in holders)
                if (kv.Value == p && EpithetMath.DefOf(kv.Key, out _) == d.defName) return true;
            return false;
        }

        public IEnumerable<EpithetDef> Of(Pawn p) => DefDatabase<EpithetDef>.AllDefsListForReading.Where(d => Holds(p, d));

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
            var firstRound = new List<string>();
            // Each colony gives its own names, from its own people (one colony: everyone, as always).
            if (!Several) Names(comp, pawns, Homes.KeyOf(Homes.Seat), quiet, firstRound);
            else
                foreach (var home in Homes.All)
                {
                    int key = Homes.KeyOf(home);
                    Names(comp, pawns.Where(p => ColonyOf(p) == key).ToList(), key, quiet, firstRound);
                }
            if (firstRound.Count > 0)
                News.Post("CC_Epithet_FirstRound".Translate(TextUtil.JoinAnd(firstRound)).Resolve(), null, NewsKind.People, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>One colony's names for the day, from its own people.</summary>
        private void Names(CouncilGameComponent comp, List<Pawn> pawns, int colony, bool quiet, List<string> firstRound)
        {
            int awarded = 0;
            foreach (var def in DefDatabase<EpithetDef>.AllDefsListForReading.InRandomOrder())
            {
                // A couple of new names a day at most; the rest can wait for tomorrow.
                if (!quiet && awarded >= 2) break;
                if (def.record == null) continue;
                var holder = Lookup(def.defName, colony);
                if (holder != null && (holder.Dead || !holder.IsColonist))
                {
                    Remove(def.defName, colony);
                    holder = null;
                }
                // A name that depends on never doing something (never breaking) is lost the day they do.
                if (holder != null && holder.records != null && !def.Allows(holder))
                {
                    Remove(def.defName, colony);
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
                    Write(def.defName, colony, best);
                    firstRound.Add(best.LabelShort + " " + def.label);
                    continue;
                }
                Award(def, best, holder, comp, colony);
                awarded++;
            }
        }

        /// <summary>
        /// A name earned some other way than by a record (a personal story's ending): given only if nobody living here
        /// holds it already. Names without a record stay with their holder.
        /// </summary>
        public bool Bestow(EpithetDef def, Pawn p, CouncilGameComponent comp)
        {
            if (def == null || p == null || comp == null || !ColonyCouncilMod.Settings.epithets) return false;
            // Given in their own colony, if nobody there holds it.
            int colony = ColonyOf(p);
            var holder = Lookup(def.defName, colony);
            if (holder != null && !holder.Dead && holder.IsColonist) return false;
            Award(def, p, null, comp, colony);
            return true;
        }

        private void Award(EpithetDef def, Pawn p, Pawn previous, CouncilGameComponent comp, int colony)
        {
            using var _cause = Causes.Push("CC_Cause_Epithet".Translate(p.LabelShort, def.label).Resolve());
            // apart: news of a name the colony gave someone; the one who lost it minds.
            Write(def.defName, colony, p);
            string text = def.earned.Formatted(p.Named("PAWN")).Resolve();
            if (previous != null) text += " " + "CC_Epithet_Taken".Translate(previous.Named("OTHER"), def.label.Named("TITLE")).Resolve();
            News.Post(text, new LookTargets(p), NewsKind.People, def.dubious ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.PositiveEvent, Importance.Notable);
            CouncilGameComponent.GiveThought(p, def.dubious ? CouncilDefOf.Council_EpithetDubious : CouncilDefOf.Council_Epithet);
            // The one who had it minds, and a rival is made, only within one colony (someone who has gone to live at another
            // has a name of theirs there to make).
            if (previous != null && !def.dubious && (!Several || EpithetMath.Rivals(ColonyOf(previous), ColonyOf(p), colony)))
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
            Scribe_Collections.Look(ref laterFounders, "laterFounders", LookMode.Value);
            Scribe_Collections.Look(ref laterFirstborn, "laterFirstborn", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (founders == null) founders = new List<string>();
                laterFounders ??= new List<string>();
                laterFirstborn ??= new List<string>();
                if (holders == null) holders = new Dictionary<string, Pawn>();
                foreach (var k in holders.Where(kv => kv.Value == null || DefDatabase<EpithetDef>.GetNamedSilentFail(EpithetMath.DefOf(kv.Key, out _)) == null).Select(kv => kv.Key).ToList())
                    holders.Remove(k);
            }
        }

        private List<string> keys;
        private List<Pawn> values;
    }
}
