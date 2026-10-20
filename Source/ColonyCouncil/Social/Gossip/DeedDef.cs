using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>How a deed reflects on whoever did it, one XML field per side of a name (<c>&lt;courage&gt;12&lt;/courage&gt;</c>).</summary>
    public class RepShifts
    {
        public float courage, kindness, diligence, skill, discretion, temper;

        public float Of(RepFacet f)
        {
            switch (f)
            {
                case RepFacet.Courage: return courage;
                case RepFacet.Kindness: return kindness;
                case RepFacet.Diligence: return diligence;
                case RepFacet.Skill: return skill;
                case RepFacet.Discretion: return discretion;
                default: return temper;
            }
        }

        public float[] ToArray() => RepMath.All.Select(Of).ToArray();

        public bool Any => RepMath.All.Any(f => Of(f) != 0f);
    }

    /// <summary>
    /// Something a colonist did that people talk about: carrying someone out under fire, running from a fight, a tantrum,
    /// a confidence kept or betrayed, a masterwork, a week of idling while the others worked. A deed is kept with who did
    /// it, when, a name for it ("the raid on the 5th of Aprimay"), how big it was, and who knows of it (those who saw it, and
    /// those who heard, from whom and in what version). What the colony knows of someone's deeds is their name in it
    /// (Social/Gossip/Reputations.cs).
    /// <para>
    /// A deed is recorded by a hook where the game or Rimfolk already notices the event (see Deeds.cs), or by one of the
    /// lists below: a memory the game gives the person it was done to (its other pawn is the doer), a history event with
    /// the doer named, a mental break the doer starts, an encounter with a stranger. Names that don't exist in this game
    /// (a DLC that isn't loaded) are simply never met.
    /// </para>
    /// </summary>
    public class DeedDef : Def
    {
        /// <summary>How it reflects on the doer, side by side, for a deed of size 1.</summary>
        public RepShifts facets = new RepShifts();
        /// <summary>What lines are written for it: lines tagged [deed:theme] are picked when this deed is told.</summary>
        public string theme;
        /// <summary>Its usual size (1 for an ordinary deed of its kind; a legendary piece is 2, a small contest 0.5).</summary>
        public float size = 1f;
        /// <summary>Its weight halves in this many days: talk dies down.</summary>
        public float fadeDays = 90f;
        /// <summary>The same deed by the same person within this many days grows the one already there (0: never merged).</summary>
        public float mergeDays;
        /// <summary>How much of a repeat's size is added to the deed it merges into.</summary>
        public float mergeShare = 0.5f;
        /// <summary>How much people want to pass it on (a scandal 2, a held line 0.6).</summary>
        public float interest = 1f;
        /// <summary>Passed on in gossip at all (false: only those who saw it know, like a colonist's own run of gossiping).</summary>
        public bool told = true;
        /// <summary>Nobody makes a joke of it (an execution); any deed cruel enough (kindness -8) is grave anyway.</summary>
        public bool grave;
        /// <summary>
        /// What they did, for lines and the character page, in the third person without the doer: "carried
        /// {VICTIM_nameDef} out of the fight". {SUBJECT_...} is the doer, {VICTIM_...} the other person in it, {DETAIL} a
        /// detail the hook gives (an office, a piece of work).
        /// </summary>
        [MustTranslate] public string text;
        /// <summary>The same without anyone else in it (a victim the game no longer has, or none).</summary>
        [MustTranslate] public string textAlone;
        /// <summary>A name for when it happened: "the tantrum on {DATE}". Fights share the fight's own name.</summary>
        [MustTranslate] public string labelText;
        /// <summary>The social log's words for it (a rule pack defining gossip_topic for the gossip conversation's lines).</summary>
        public RulePackDef logTopic;

        // ---------------- What records it ----------------

        /// <summary>Memories (ThoughtDef names) the game gives the one it was done to; the memory's other pawn is the doer.</summary>
        public List<string> memories;
        /// <summary>History events (HistoryEventDef names) that name the doer.</summary>
        public List<string> historyEvents;
        /// <summary>Mental breaks (MentalStateDef names) the doer starts.</summary>
        public List<string> mentalStates;
        /// <summary>Encounters with strangers (EncounterDef names) in which the colonist did it.</summary>
        public List<string> encounters;
        /// <summary>Only while a fight is on the doer's map.</summary>
        public bool duringFight;
        /// <summary>Only while no fight is on (a contest, a breakup).</summary>
        public bool notInFight;
        /// <summary>Only when the doer was in an aggressive break at the time.</summary>
        public bool doerInBreak;
        /// <summary>Only when the one it was done to was a prisoner, a guest held, or someone not of the colony.</summary>
        public bool victimOutsider;
        /// <summary>Only while food runs short (a few days' worth, or a hunger in the world).</summary>
        public bool duringHunger;
        /// <summary>The size instead, when the doer had been drafted (ran with a weapon in hand); 0: no different.</summary>
        public float draftedSize;

        [Unsaved] private float[] shiftArray;
        [Unsaved] private int valence = 2;

        /// <summary>The shifts in <see cref="RepFacet"/> order (worked out once).</summary>
        public float[] Shifts => shiftArray ??= facets?.ToArray() ?? new float[RepMath.Facets];

        /// <summary>Good (+1), bad (-1) or neither (0) for the doer's name.</summary>
        public int Valence
        {
            get
            {
                if (valence == 2) valence = RepMath.Valence(Shifts);
                return valence;
            }
        }

        /// <summary>Not to be joked about.</summary>
        public bool Grave => grave || RepMath.Grave(Shifts);

        /// <summary>The theme as a line tag: deed:rescue.</summary>
        public string Tag => "deed:" + (theme ?? defName);

        public static List<DeedDef> All => DefDatabase<DeedDef>.AllDefsListForReading;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (facets == null || !facets.Any) yield return "a deed that says nothing about anyone (no facets)";
            if (text.NullOrEmpty()) yield return "no text";
            if (theme.NullOrEmpty()) yield return "no theme";
            if (size <= 0f) yield return "size must be above 0";
            if (fadeDays <= 0f) yield return "fadeDays must be above 0";
            if (duringFight && notInFight) yield return "duringFight and notInFight both set";
        }
    }
}
