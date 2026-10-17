using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Someone who knows of a deed: they saw it (<see cref="from"/> -1), or heard it from someone, in a version that may
    /// have grown or shrunk in the telling, and believe it so far.
    /// </summary>
    public class DeedKnower : IExposable
    {
        /// <summary>Who knows (thingIDNumber), with their name in case they're gone.</summary>
        public int id;
        public string name;
        /// <summary>Who told them (-1: they saw it themselves).</summary>
        public int from = -1;
        public string fromName;
        public int tick;
        /// <summary>How big they think it was (the deed's size, as the story reached them).</summary>
        public float version;
        /// <summary>How far they believe it, 0.2..1 (1 for a witness).</summary>
        public float belief = 1f;
        /// <summary>How the one who told them coloured it.</summary>
        public Slant slant;

        public bool Witness => from < 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref from, "from", -1);
            Scribe_Values.Look(ref fromName, "fromName");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref version, "version");
            Scribe_Values.Look(ref belief, "belief", 1f);
            Scribe_Values.Look(ref slant, "slant");
        }
    }

    /// <summary>Something a colonist did (<see cref="DeedDef"/>): who, when, what it was called, how big, and who knows of it.</summary>
    public class Deed : IExposable
    {
        public int id;
        public DeedDef def;
        /// <summary>Who did it (thingIDNumber), with their name.</summary>
        public int doer;
        public string doerName;
        /// <summary>The other person in it (the one rescued, hurt, beaten, betrayed), or -1.</summary>
        public int victim = -1;
        public string victimName;
        /// <summary>When it happened, and when it last grew (a merged repeat).</summary>
        public int tick;
        public int lastTick;
        /// <summary>A name for it: "the raid on 5th of Aprimay".</summary>
        public string label;
        /// <summary>A detail the hook gave (an office won, a piece of work made), for {DETAIL} in its text.</summary>
        public string detail;
        /// <summary>How big it really was.</summary>
        public float size = 1f;
        /// <summary>Everyone who knows of it, in the order they learned.</summary>
        public List<DeedKnower> heard = new List<DeedKnower>();

        public DeedKnower KnowerOf(int pawnId)
        {
            for (int i = 0; i < heard.Count; i++)
                if (heard[i].id == pawnId)
                    return heard[i];
            return null;
        }

        public bool Knows(int pawnId) => KnowerOf(pawnId) != null;

        /// <summary>How much of it is left, 0..1, after the time since it last grew.</summary>
        public float Fade(int now) => RepMath.Faded(1f, (now - lastTick) / (float)GenDate.TicksPerDay, def?.fadeDays ?? 90f);

        /// <summary>Its size now, faded.</summary>
        public float Weight(int now) => size * Fade(now);

        public float AgeDays(int now) => (now - tick) / (float)GenDate.TicksPerDay;

        public void ExposeData()
        {
            bool lost = false;
            Scribe_Values.Look(ref id, "id");
            CouncilScribe.LookDef(ref def, "def", ref lost);
            Scribe_Values.Look(ref doer, "doer");
            Scribe_Values.Look(ref doerName, "doerName");
            Scribe_Values.Look(ref victim, "victim", -1);
            Scribe_Values.Look(ref victimName, "victimName");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref lastTick, "lastTick");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref detail, "detail");
            Scribe_Values.Look(ref size, "size", 1f);
            Scribe_Collections.Look(ref heard, "heard", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                heard ??= new List<DeedKnower>();
                heard.RemoveAll(k => k == null);
                if (lastTick <= 0) lastTick = tick;
            }
        }
    }
}
