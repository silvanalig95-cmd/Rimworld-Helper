using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One god of the pantheon, saved with the game (Gods/GodBook.cs): who it is (its name and epithet, its domains, the form
    /// its people picture it in, its kind and nature), who holds it (peoples and faiths), where it came from (an Ideology
    /// deity, Rimfolk's makers, a people's patron, the old places, the colony's own faith), its colours and symbol and the
    /// look the presence part draws it in, and whether the colony knows it yet.
    /// </summary>
    public class God : IExposable
    {
        public int id;
        public string name;
        /// <summary>A noun phrase after its name: "the Storm-father", "the Walker of the Dead".</summary>
        public string epithet;
        public GodDomain domain, second;
        public GodForm form;
        /// <summary>How it works now (an archotech, the Hunger, fading...), and the kind it has by its lore, to come back to when worshipped again.</summary>
        public GodKind kind, baseKind;
        public GodNature nature;
        public Gender gender;
        /// <summary>The age of the people it was made for (0 tribal to 3 spacer).</summary>
        public int age;
        /// <summary>The peoples and faiths that hold it now.</summary>
        public List<Faction> factions = new List<Faction>();
        public List<Ideo> ideos = new List<Ideo>();
        /// <summary>Where it came from: "deity" (Ideology), "maker" (Rimfolk's makers), "patron", "old" (the old places), "newborn".</summary>
        public string source;
        /// <summary>The Ideology deity it came from: its faith's id and its name ("12|Aranea"), and its type as the faith has it.</summary>
        public string deityKey, deityType;
        /// <summary>The patron it is (GodPatronDef's defName), or null.</summary>
        public string patron;
        public Color colour = Color.white, colour2 = Color.gray;
        /// <summary>The look the presence part draws it in (a GodLookDef's defName: Rimfolk_GodLook_&lt;Domain&gt;).</summary>
        public string look;
        /// <summary>The colony knows it (from its own faith, its neighbours, letters, visitors), how, and since when.</summary>
        public bool known;
        public string knownHow;
        public int knownAt = -1;
        /// <summary>When it came into the pantheon, and when a people or faith last held it.</summary>
        public int born, lastHeld;
        /// <summary>A newborn's growth with the colony's devotion (GodsMath.NewbornGrown grows it up).</summary>
        public float devotion;

        /// <summary>Its domain's def (colours, symbol, sacred things), or null.</summary>
        public GodDomainDef DomainDef => GodDomainDef.Of(domain);

        /// <summary>Its patron def, or null.</summary>
        public GodPatronDef PatronDef => patron.NullOrEmpty() ? null : DefDatabase<GodPatronDef>.GetNamedSilentFail(patron);

        /// <summary>The texture path of its symbol (its domain's).</summary>
        public string SymbolPath => DomainDef?.symbol;

        /// <summary>Someone still holds it.</summary>
        public bool Worshipped => factions.Count > 0 || ideos.Count > 0;

        public override string ToString() => name ?? "god " + id;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref epithet, "epithet");
            Scribe_Values.Look(ref domain, "domain", GodDomain.None);
            Scribe_Values.Look(ref second, "second", GodDomain.None);
            Scribe_Values.Look(ref form, "form", GodForm.OldGod);
            Scribe_Values.Look(ref kind, "kind", GodKind.Ordinary);
            Scribe_Values.Look(ref baseKind, "baseKind", GodKind.Ordinary);
            Scribe_Values.Look(ref nature, "nature", GodNature.Patient);
            Scribe_Values.Look(ref gender, "gender", Gender.None);
            Scribe_Values.Look(ref age, "age", 1);
            Scribe_Collections.Look(ref factions, "factions", LookMode.Reference);
            Scribe_Collections.Look(ref ideos, "ideos", LookMode.Reference);
            Scribe_Values.Look(ref source, "source");
            Scribe_Values.Look(ref deityKey, "deityKey");
            Scribe_Values.Look(ref deityType, "deityType");
            Scribe_Values.Look(ref patron, "patron");
            Scribe_Values.Look(ref colour, "colour", Color.white);
            Scribe_Values.Look(ref colour2, "colour2", Color.gray);
            Scribe_Values.Look(ref look, "look");
            Scribe_Values.Look(ref known, "known");
            Scribe_Values.Look(ref knownHow, "knownHow");
            Scribe_Values.Look(ref knownAt, "knownAt", -1);
            Scribe_Values.Look(ref born, "born");
            Scribe_Values.Look(ref lastHeld, "lastHeld");
            Scribe_Values.Look(ref devotion, "devotion");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                factions ??= new List<Faction>();
                factions.RemoveAll(f => f == null);
                ideos ??= new List<Ideo>();
                ideos.RemoveAll(i => i == null);
                if (look.NullOrEmpty()) look = "Rimfolk_GodLook_" + domain;
            }
        }
    }

    /// <summary>A tie between two gods (Gods/GodTiesMath.cs): rivals, lovers, kin, allies, an old wrong; the first god leads where it matters.</summary>
    public class GodTie : IExposable
    {
        public int a, b;
        public GodTieKind kind;
        /// <summary>How strong it is, 0.1 to 1.</summary>
        public float strength = 0.5f;
        /// <summary>When it came to be what it is.</summary>
        public int since;

        /// <summary>The other god of the tie, from one of them.</summary>
        public int Other(int god) => god == a ? b : god == b ? a : -1;

        public bool Has(int god) => a == god || b == god;

        public void ExposeData()
        {
            Scribe_Values.Look(ref a, "a");
            Scribe_Values.Look(ref b, "b");
            Scribe_Values.Look(ref kind, "kind", GodTieKind.Rivals);
            Scribe_Values.Look(ref strength, "strength", 0.5f);
            Scribe_Values.Look(ref since, "since");
        }
    }

    /// <summary>The colony's standing with a god at one of its homes, and when it was last set (it drifts from there).</summary>
    public class GodStanding : IExposable
    {
        public int god;
        /// <summary>The home (Homes.KeyOf).</summary>
        public int home = HomesMath.None;
        public float value;
        public int tick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref god, "god");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref value, "value");
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    /// <summary>What a remembered act of a god was (GodAct.kind).</summary>
    public enum GodActKind
    {
        /// <summary>The colony's standing with it moved (a deed, a sacrilege, an offering).</summary>
        Favour,
        /// <summary>The presence part's kinds of a god's coming (PresenceKind).</summary>
        Appear,
        Act,
        Reveal,
        Depart,
        Blessing,
        Wrath,
        Omen,
        /// <summary>A tie between it and another god changed.</summary>
        Tie,
        /// <summary>It came into the pantheon (a newborn, a new people's god).</summary>
        Born,
        /// <summary>It lost its last worshippers, or fell silent.</summary>
        Faded,
        /// <summary>Its people came back to it.</summary>
        Returned,
        /// <summary>The colony came to know it.</summary>
        Known,
        /// <summary>A mark it left on the land.</summary>
        Mark,
        /// <summary>A vow to it made, kept or broken.</summary>
        Vow
    }

    /// <summary>
    /// One act of a god the colony remembers (Gods.Remember): which god (and another, for a tie), at which home, when, what
    /// it was, its words, the favour it moved and where on the map (a mark, an appearance). Shown on the god's page.
    /// </summary>
    public class GodAct : IExposable
    {
        public int god = -1, other = -1;
        public int home = HomesMath.None;
        public int tick;
        public GodActKind kind;
        /// <summary>What happened, in words, already filled in.</summary>
        public string text;
        /// <summary>The favour it moved with the colony (0: none); never shown as a number.</summary>
        public float favour;
        public IntVec3 cell = IntVec3.Invalid;

        public void ExposeData()
        {
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref other, "other", -1);
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref kind, "kind", GodActKind.Favour);
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref favour, "favour");
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
        }
    }

    /// <summary>
    /// A vow a colonist made to a god (stage 2's wishes and life goals make them): what it is in words, since and until
    /// when, and whether it was kept or broken. Keeping one pleases the god; breaking one offends a patient or cold one most.
    /// </summary>
    public class GodVow : IExposable
    {
        public int god;
        public Pawn pawn;
        public int home = HomesMath.None;
        public string what;
        public int since, until = -1;
        public bool kept, broken;

        public bool Open => !kept && !broken;

        public void ExposeData()
        {
            Scribe_Values.Look(ref god, "god");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref what, "what");
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref until, "until", -1);
            Scribe_Values.Look(ref kept, "kept");
            Scribe_Values.Look(ref broken, "broken");
        }
    }

    /// <summary>A deed in a domain at a home, kept a year: what a newborn god is born of.</summary>
    public class GodDeed : IExposable
    {
        public GodDomain domain;
        public int home = HomesMath.None;
        public int tick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref domain, "domain", GodDomain.None);
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref tick, "tick");
        }
    }
}
