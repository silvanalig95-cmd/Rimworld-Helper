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
    /// Each colonist's diary: a line in their own voice whenever something happens to them. Kept by ThingID and
    /// name, so the dead keep their journals.
    /// </summary>
    public class JournalTracker : IExposable
    {
        public const int MaxEntries = 60;
        private List<PawnJournal> journals = new List<PawnJournal>();
        /// <summary>Grief memories already written about (memory def + other pawn), so each loss is written once.</summary>
        private HashSet<string> griefSeen = new HashSet<string>();

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
        public void Write(Pawn p, string pool, Wish w = null, params NamedArgument[] args)
        {
            if (!ColonyCouncilMod.Settings.journals || p == null || p.Dead || !p.RaceProps.Humanlike) return;
            string line;
            try
            {
                line = Flavor.FromPool(pool, p, w);
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

        private int lastThreatWritten;

        /// <summary>Once a day: first entries, raids survived, and losses they haven't written about yet.</summary>
        public void Daily()
        {
            if (!ColonyCouncilMod.Settings.journals) return;
            var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Baby()).ToList();
            foreach (var p in colonists)
            {
                if (For(p) != null || p.records == null) continue;
                bool newcomer = p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) < GenDate.TicksPerDay * 3;
                Write(p, newcomer ? "Council_Journal_Arrival" : "Council_Journal_Begin");
            }
            var map = Find.AnyPlayerHomeMap;
            int threat = map?.storyState.LastThreatBigTick ?? 0;
            if (threat > lastThreatWritten && Find.TickManager.TicksGame - threat < GenDate.TicksPerDay * 2)
            {
                lastThreatWritten = threat;
                var writers = colonists.Where(p => p.MapHeld == map)
                    .OrderByDescending(p => (p.health.hediffSet.hediffs.Any(h => h is Hediff_Injury) ? 2f : 0f) + Rand.Value)
                    .Take(2);
                foreach (var p in writers) Write(p, "Council_Journal_Raid");
            }
            foreach (var p in colonists)
            {
                var mem = p.needs?.mood?.thoughts?.memories?.Memories;
                if (mem == null) continue;
                foreach (var m in mem)
                {
                    if (!Flavor.GriefThoughts.Contains(m.def.defName) || m.otherPawn == null || m.age > GenDate.TicksPerDay * 2) continue;
                    string key = p.ThingID + "|" + m.def.defName + "|" + m.otherPawn.ThingID;
                    if (!griefSeen.Add(key)) continue;
                    Write(p, "Council_Journal_Grief", null, m.otherPawn.Named("OTHER"));
                }
            }
            if (griefSeen.Count > 500) griefSeen.Clear();
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref journals, "journals", LookMode.Deep);
            Scribe_Collections.Look(ref griefSeen, "griefSeen", LookMode.Value);
            Scribe_Values.Look(ref lastThreatWritten, "lastThreatWritten");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (journals == null) journals = new List<PawnJournal>();
                journals.RemoveAll(j => j == null || j.pawnId == null);
                if (griefSeen == null) griefSeen = new HashSet<string>();
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
