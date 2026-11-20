using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>What is said in turn at a gathering: each person's aim (A27), or the year's lines by its writer (A29).</summary>
    public enum ReadingKind
    {
        /// <summary>The year's first supper: each at the table with a life goal says it, one line each.</summary>
        Aims,
        /// <summary>The year read aloud: its writer reads a few lines of the year letter, in order.</summary>
        Year
    }

    /// <summary>A reading at one gathering (a PlayOut, by its id), and how far it has got.</summary>
    public class GatheringReading : IExposable
    {
        public int playId = -1;
        public ReadingKind kind;
        /// <summary>The year: who reads it, and its lines in order.</summary>
        public Pawn speaker;
        public List<string> lines = new List<string>();
        public int next;
        /// <summary>No line before this tick (a pause between lines).</summary>
        public int nextTick;
        /// <summary>The year's writer has not been at the table for this many looks: their next line is passed over after a few.</summary>
        public int missed;
        /// <summary>The aims: someone without a life goal may choose one there and then ("everyone says it").</summary>
        public bool chooseNew;
        /// <summary>The aims: who has had their turn, and the colony's year they're said in.</summary>
        public List<Pawn> spoke = new List<Pawn>();
        public int year;
        /// <summary>The aims: what each said (pawn ThingID), and those who backed them there, for the closeness at the end.</summary>
        public List<string> said = new List<string>();
        /// <summary>The year: its ordinal ("second"), for the shared memory's label.</summary>
        public string label;

        public void ExposeData()
        {
            Scribe_Values.Look(ref playId, "playId", -1);
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref speaker, "speaker");
            Scribe_Collections.Look(ref lines, "lines", LookMode.Value);
            Scribe_Values.Look(ref next, "next");
            Scribe_Values.Look(ref nextTick, "nextTick");
            Scribe_Values.Look(ref missed, "missed");
            Scribe_Values.Look(ref chooseNew, "chooseNew");
            Scribe_Collections.Look(ref spoke, "spoke", LookMode.Reference);
            Scribe_Values.Look(ref year, "year");
            Scribe_Collections.Look(ref said, "said", LookMode.Value);
            Scribe_Values.Look(ref label, "label");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lines ??= new List<string>();
                spoke ??= new List<Pawn>();
                spoke.RemoveAll(p => p == null);
                said ??= new List<string>();
            }
        }
    }

    /// <summary>
    /// Lines said in turn at a gathering under way, by the people they belong to and only while they are at it (in the
    /// gathering's own area, awake and up): at the year's first supper each at the table says what they mean to do this
    /// year (A27, <see cref="FirstSupper"/>), and on the landing's anniversary the year's writer reads a few lines of the
    /// year letter aloud (A29, <see cref="YearRead"/>). A line every so often (<see cref="ReadingMath.GapTicks"/>), so
    /// the bubbles can be read; whoever isn't there yet is waited for a while, then passed over. What those there are left
    /// with comes when the gathering is over (<see cref="Closed"/>).
    /// </summary>
    public static class Readings
    {
        private static readonly Color SpeechColor = new Color(1f, 0.93f, 0.72f);

        /// <summary>Starts a reading at a gathering just set going.</summary>
        public static GatheringReading Add(PlayOut p, ReadingKind kind)
        {
            var book = LifeBook.Instance;
            if (book == null || p == null) return null;
            book.readings.RemoveAll(r => r.playId == p.id);
            var r = new GatheringReading { playId = p.id, kind = kind };
            book.readings.Add(r);
            while (book.readings.Count > LifeBook.MaxReadings) book.readings.RemoveAt(0);
            return r;
        }

        /// <summary>The reading at this gathering, or null.</summary>
        public static GatheringReading At(PlayOut p)
        {
            var book = LifeBook.Instance;
            return book == null || p == null ? null : book.readings.FirstOrDefault(r => r.playId == p.id);
        }

        public static void Step(LifeBook book, int now)
        {
            var tracker = PlayOutTracker.Instance;
            foreach (var r in book.readings.ToList())
            {
                var p = tracker?.Get(r.playId);
                // Over without happening (fell through, or never began): nothing was said.
                if (p == null || p.state == PlayState.Done)
                {
                    book.readings.Remove(r);
                    continue;
                }
                if (p.state != PlayState.Running || !p.IsGathering || now < r.nextTick) continue;
                var job = LordOf(p);
                if (job == null) continue;
                var map = p.map;
                var here = job.lord.ownedPawns.Where(x => x != null && x.Spawned && x.Map == map && Together.Able(x)
                                                          && GatheringsUtility.InGatheringArea(x.Position, job.Spot, map)).ToList();
                if (here.Count < 2) continue;
                bool said = r.kind == ReadingKind.Aims ? FirstSupper.SayNext(r, here, map) : YearRead.ReadNext(r, here);
                r.nextTick = now + (said ? ReadingMath.GapTicks : ReadingMath.LookAgainTicks);
            }
        }

        /// <summary>The gathering happened: what those there are left with (the aims said bring the speakers and their backers closer; the year read is remembered together).</summary>
        public static void Closed(LifeBook book, PlayOut p, List<Pawn> took)
        {
            var r = book.readings.FirstOrDefault(x => x.playId == p.id);
            if (r == null) return;
            book.readings.Remove(r);
            if (r.kind == ReadingKind.Aims) FirstSupper.Over(r, p, took);
            else YearRead.Over(r, p, took);
        }

        /// <summary>The gathering's lord, while it's on.</summary>
        public static LordJob_PlayOut LordOf(PlayOut p)
        {
            var lords = p?.map?.lordManager?.lords;
            if (lords == null) return null;
            foreach (var l in lords)
                if (l?.LordJob is LordJob_PlayOut j && j.playId == p.id)
                    return j;
            return null;
        }

        /// <summary>A line said at the table, over the speaker's head; to someone there when <paramref name="to"/> is given.</summary>
        internal static bool Say(Pawn speaker, string line, Pawn to = null)
        {
            // together: said at the gathering itself, by someone standing in its area with the others (Step's own look).
            if (speaker == null || line.NullOrEmpty()) return false;
            return Speech.Say(speaker, line, to, SpeechColor);
        }
    }
}
