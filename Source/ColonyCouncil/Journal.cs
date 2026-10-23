using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    public class JournalEntry : IExposable
    {
        public int tick;
        public string date;
        public string text;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref date, "date");
            Scribe_Values.Look(ref text, "text");
        }
    }

    public class PawnJournal : IExposable
    {
        public string pawnId;
        public string name;
        public List<JournalEntry> entries = new List<JournalEntry>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref name, "name");
            Scribe_Collections.Look(ref entries, "entries", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && entries == null) entries = new List<JournalEntry>();
        }
    }

    /// <summary>
    /// Each colonist's diary: a line in their own voice whenever something happens to them, and now and then about
    /// an ordinary day (<see cref="EverydayJournal"/>). Kept by ThingID and name, so the dead keep their journals.
    /// </summary>
    public class JournalTracker : IExposable
    {
        public const int MaxEntries = 60;
        private List<PawnJournal> journals = new List<PawnJournal>();

        public IEnumerable<PawnJournal> All => journals.Where(j => j.entries.Count > 0);
        /// <summary>Grief memories already written about (memory def + other pawn), so each loss is written once.</summary>
        private HashSet<string> griefSeen = new HashSet<string>();
        /// <summary>Everyday moments already written about: "pawnId|moment" -> tick.</summary>
        private Dictionary<string, int> everydaySeen = new Dictionary<string, int>();

        /// <summary>Journals of people no longer with the colony (dead, gone, walked out).</summary>
        public IEnumerable<PawnJournal> Departed()
        {
            var here = People.OursIds();
            return journals.Where(j => j.entries.Count > 0 && !here.Contains(j.pawnId));
        }

        public PawnJournal For(Pawn p, bool create = false)
        {
            if (p == null) return null;
            var j = journals.FirstOrDefault(x => x.pawnId == p.ThingID);
            if (j == null && create)
            {
                j = new PawnJournal { pawnId = p.ThingID, name = p.LabelShort };
                journals.Add(j);
            }
            if (j != null) j.name = p.LabelShort;
            return j;
        }

        /// <summary>
        /// Writes a line from <paramref name="pool"/> in the colonist's voice. The pool's lines may use {PAWN_...}
        /// and whatever named arguments are passed (WISH, OTHER_..., TITLE, DECREE, DILEMMA, YEARS).
        /// </summary>
        public void Write(Pawn p, string pool, Wish w = null, params NamedArgument[] args) => WriteTagged(p, pool, null, w, args);

        /// <summary>Like <see cref="Write"/>, with extra tags for picking the line (what the council answered, say).</summary>
        public void WriteTagged(Pawn p, string pool, string[] tags, Wish w = null, params NamedArgument[] args)
        {
            if (!ColonyCouncilMod.Settings.journals || p == null || p.Dead || !p.RaceProps.Humanlike) return;
            string line;
            try
            {
                line = Flavor.FromPool(pool, p, w, null, tags);
                if (line.NullOrEmpty()) return;
                var all = new List<NamedArgument> { p.Named("PAWN") };
                if (w != null) all.Add(w.Label.Named("WISH"));
                if (args != null) all.AddRange(args);
                line = line.Formatted(all.ToArray()).Resolve();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("journal " + pool, e);
                return;
            }
            WriteText(p, line);
        }

        /// <summary>Adds a line already written in the colonist's voice (a dream, say).</summary>
        public void WriteText(Pawn p, string line)
        {
            if (!ColonyCouncilMod.Settings.journals || p == null || p.Dead || !p.RaceProps.Humanlike || line.NullOrEmpty()) return;
            var j = For(p, true);
            j.entries.Add(new JournalEntry { tick = Find.TickManager.TicksGame, date = DateNow(p), text = line });
            if (j.entries.Count > MaxEntries) j.entries.RemoveAt(0);
        }

        private static string DateNow(Pawn p)
        {
            var map = p.MapHeld ?? Find.AnyPlayerHomeMap;
            var loc = map != null ? Find.WorldGrid.LongLatOf(map.Tile) : Vector2.zero;
            return GenDate.DateFullStringAt(Find.TickManager.TicksAbs, loc);
        }

        // ---------------- One happening, one line ----------------

        /// <summary>A happening's lines come in the same moment (a wedding, an election, a birth): within this many ticks.</summary>
        public const int SameHappeningTicks = 120;

        /// <summary>
        /// Whether <paramref name="p"/> already has a line from the last <paramref name="ticks"/> ticks: a happening that
        /// several systems notice (an election: the council's line and the mark it leaves) is written once.
        /// </summary>
        public bool WroteWithin(Pawn p, int ticks = SameHappeningTicks)
        {
            var j = For(p);
            if (j == null || j.entries.Count == 0) return false;
            int last = j.entries[j.entries.Count - 1].tick;
            int now = Find.TickManager.TicksGame;
            return last <= now && now - last <= ticks;
        }

        /// <summary>
        /// The first word on a loss: true (and remembered) the first time <paramref name="p"/> writes about
        /// <paramref name="lost"/> dying, false after that. A death is felt through several memories (a brother, a friend,
        /// a colonist) and noticed by several systems (the mark it leaves, the grief, the bond lost, the group), and is
        /// written about once.
        /// </summary>
        public bool FirstOnLoss(Pawn p, Pawn lost)
        {
            if (p == null || lost == null) return true;
            return griefSeen.Add(p.ThingID + "|lost|" + lost.ThingID);
        }

        private int lastThreatWritten;

        /// <summary>When a journal's last line was written (0 for an empty one).</summary>
        private static int LastTick(PawnJournal j) => j.entries.Count > 0 ? j.entries[j.entries.Count - 1].tick : 0;

        /// <summary>
        /// The journals of those no longer with the colony: at most <see cref="Caps.DepartedJournals"/>, the ones written in
        /// most lately. Everyone still ours keeps theirs, however many.
        /// </summary>
        public void Prune()
        {
            var here = People.OursIds();
            int ours = 0;
            foreach (var j in journals)
                if (here.Contains(j.pawnId))
                    ours++;
            Caps.TrimByAge(journals, ours + Caps.DepartedJournals, LastTick, j => !here.Contains(j.pawnId));
        }

        /// <summary>
        /// Like <see cref="Write"/>, with the hour tags ([night], [morning]) of when the moment happened, <paramref name="age"/>
        /// ticks ago, rather than of the daily pass, which comes at the same hour every day.
        /// </summary>
        private void WriteAt(Pawn p, int age, string pool, params NamedArgument[] args)
        {
            Flavor.AtHour = EverydayJournal.HourOf(p, age);
            try
            {
                Write(p, pool, null, args);
            }
            finally
            {
                Flavor.AtHour = -1;
            }
        }

        /// <summary>Once a day: first entries, raids survived, and losses they haven't written about yet.</summary>
        public void Daily()
        {
            Prune();
            if (!ColonyCouncilMod.Settings.journals) return;
            var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Baby()).ToList();
            foreach (var p in colonists)
            {
                if (For(p) != null || p.records == null) continue;
                bool newcomer = p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) < GenDate.TicksPerDay * 3;
                // A newcomer's first entry goes by the hour they came; an old hand's by some hour of the past day.
                int age = newcomer ? (int)p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) : Rand.Range(1, GenDate.TicksPerDay);
                WriteAt(p, age, newcomer ? "Council_Journal_Arrival" : "Council_Journal_Begin");
            }
            var map = Find.AnyPlayerHomeMap;
            int threat = map?.storyState.LastThreatBigTick ?? 0;
            if (threat > lastThreatWritten && Find.TickManager.TicksGame - threat < GenDate.TicksPerDay * 2)
            {
                lastThreatWritten = threat;
                // Not those who already wrote about the fight in their own words (it marked them: Persona/Marks.cs).
                var writers = colonists.Where(p => p.MapHeld == map && !WroteWithin(p, GenDate.TicksPerDay))
                    .OrderByDescending(p => (p.health.hediffSet.hediffs.Any(h => h is Hediff_Injury) ? 2f : 0f) + Rand.Value)
                    .Take(2);
                foreach (var p in writers) WriteAt(p, Find.TickManager.TicksGame - threat, "Council_Journal_Raid");
            }
            foreach (var p in colonists)
            {
                var mem = p.needs?.mood?.thoughts?.memories?.Memories;
                if (mem == null) continue;
                foreach (var m in mem)
                {
                    if (!Flavor.GriefThoughts.Contains(m.def.defName) || m.otherPawn == null || m.age > GenDate.TicksPerDay * 2) continue;
                    string key = p.ThingID + "|" + m.def.defName + "|" + m.otherPawn.ThingID;
                    if (!griefSeen.Add(key) || !FirstOnLoss(p, m.otherPawn)) continue;
                    WriteAt(p, m.age, "Council_Journal_Grief", m.otherPawn.Named("OTHER"));
                }
            }
            if (griefSeen.Count > 500) griefSeen.Clear();
            EverydayJournal.Daily(this, colonists, everydaySeen);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref journals, "journals", LookMode.Deep);
            Scribe_Collections.Look(ref griefSeen, "griefSeen", LookMode.Value);
            Scribe_Values.Look(ref lastThreatWritten, "lastThreatWritten");
            Scribe_Collections.Look(ref everydaySeen, "everydaySeen", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (journals == null) journals = new List<PawnJournal>();
                journals.RemoveAll(j => j == null || j.pawnId == null);
                if (griefSeen == null) griefSeen = new HashSet<string>();
                if (everydaySeen == null) everydaySeen = new Dictionary<string, int>();
            }
        }
    }

    /// <summary>A colonist's journal, newest first.</summary>
    public class Dialog_Journal : Window
    {
        private readonly PawnJournal journal;
        private Vector2 scroll;
        private float height = 400f;

        public Dialog_Journal(PawnJournal journal)
        {
            this.journal = journal;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        public override Vector2 InitialSize => new Vector2(560f, 640f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 30f, 34f), "CC_Journal_Title".Translate(journal.name));
            Text.Font = GameFont.Small;
            var outRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            if (journal.entries.Count == 0) Widgets.Label(new Rect(0f, 0f, view.width, 30f), "CC_Journal_Empty".Translate());
            for (int i = journal.entries.Count - 1; i >= 0; i--)
            {
                var e = journal.entries[i];
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(0f, y, view.width, 18f), e.date);
                Text.Font = GameFont.Small;
                GUI.color = new Color(1f, 0.95f, 0.85f);
                float h = Text.CalcHeight(e.text, view.width - 10f);
                Widgets.Label(new Rect(10f, y + 18f, view.width - 10f, h), e.text);
                GUI.color = Color.white;
                y += 18f + h + 12f;
            }
            height = Mathf.Max(y, 50f);
            Widgets.EndScrollView();
        }
    }
}
