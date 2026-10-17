using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A move on each side of a person, written in XML as one field per side (<c>&lt;warmth&gt;20&lt;/warmth&gt;</c>).</summary>
    public class PersonaShifts
    {
        public float warmth, nerve, temper, humour, trust, pride, openness, outlook;

        public float Of(PersonaAxis a)
        {
            switch (a)
            {
                case PersonaAxis.Warmth: return warmth;
                case PersonaAxis.Nerve: return nerve;
                case PersonaAxis.Temper: return temper;
                case PersonaAxis.Humour: return humour;
                case PersonaAxis.Trust: return trust;
                case PersonaAxis.Pride: return pride;
                case PersonaAxis.Openness: return openness;
                default: return outlook;
            }
        }

        public bool Any => PersonaMath.All.Any(a => Of(a) != 0f);
    }

    /// <summary>One trait (at one degree) and where it sets a person on each side.</summary>
    public class PersonaTraitEntry : PersonaShifts
    {
        /// <summary>The trait's defName; a trait from a mod that isn't loaded is simply skipped.</summary>
        public string trait;
        /// <summary>The trait's degree (Optimist is NaturalMood 1); 0 for traits without degrees.</summary>
        public int degree;

        [Unsaved] private TraitDef def;
        [Unsaved] private bool looked;

        public TraitDef Def
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    def = trait.NullOrEmpty() ? null : DefDatabase<TraitDef>.GetNamedSilentFail(trait);
                }
                return def;
            }
        }
    }

    /// <summary>
    /// How traits set a person's nature (<see cref="PersonaAxis"/>). Any number of these may exist: the vanilla one,
    /// and one per mod with traits of its own; all of them count.
    /// </summary>
    public class PersonaNatureDef : Def
    {
        public List<PersonaTraitEntry> traits = new List<PersonaTraitEntry>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (traits == null) yield break;
            foreach (var t in traits)
            {
                if (t == null || t.trait.NullOrEmpty()) yield return "an entry without a trait";
                else if (!t.Any) yield return "trait " + t.trait + " moves nothing";
            }
        }

        public static List<PersonaNatureDef> All => DefDatabase<PersonaNatureDef>.AllDefsListForReading;
    }

    /// <summary>
    /// Something that happened to a person and changed them: a mark. Part of the change fades over a year or so
    /// (<see cref="PersonaMath.DriftHalfLifeDays"/>), part of it stays (<see cref="lastingShare"/>). The mark itself is
    /// kept on the person for a while, so the game can say what shaped them and they can bring it up.
    /// </summary>
    public class PersonaMarkDef : Def
    {
        /// <summary>How the mark moves the person.</summary>
        public PersonaShifts shifts = new PersonaShifts();
        /// <summary>The share of the move that never fades.</summary>
        public float lastingShare = 0.3f;
        /// <summary>A turning point: kept longer and listed first.</summary>
        public bool major;
        /// <summary>How long the mark stays listed on the person (0: for good).</summary>
        public int keepDays = 120;
        /// <summary>
        /// What shaped them, as the game says it: "{PAWN_nameDef} has kept a weapon close since {LABEL}." {OTHER} is the
        /// other person in it (by name), {LABEL} what it was about.
        /// </summary>
        [MustTranslate] public string shaped;
        /// <summary>A short name for it ("the night the walls fell"), when the caller gives no label of its own.</summary>
        [MustTranslate] public string defaultLabel;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (shifts == null || !shifts.Any) yield return "a mark that moves nothing";
            if (lastingShare < 0f || lastingShare > 1f) yield return "lastingShare must be 0..1";
            if (shaped.NullOrEmpty()) yield return "no 'shaped' text";
        }
    }

    /// <summary>
    /// A kind of thing two people went through together, which either of them may bring up later ("Remember the
    /// boomalope?"). Saved per pair (<see cref="SharedMemory"/>).
    /// </summary>
    public class SharedMemoryDef : Def
    {
        /// <summary>The pair's history as the game shows it: "held the line together at {LABEL}".</summary>
        [MustTranslate] public string text;
        /// <summary>How much it matters next to their other memories.</summary>
        public float weight = 1f;
        /// <summary>Its weight halves in this many days (0: it never fades).</summary>
        public int fadeDays = 60;
        /// <summary>Good (+1), bad (-1) or both (0) to remember.</summary>
        public int valence = 1;
        /// <summary>A joke between them: it gets funnier for a while each time it comes up, then wears out.</summary>
        public bool joke;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (text.NullOrEmpty()) yield return "no text";
            if (valence < -1 || valence > 1) yield return "valence must be -1, 0 or 1";
        }
    }
}
