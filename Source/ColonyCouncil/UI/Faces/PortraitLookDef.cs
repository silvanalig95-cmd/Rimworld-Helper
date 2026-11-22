using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Something on a person a picture can show, beyond their kind, sex and age (docs: DESIGN.md, "Portraits, phase 5:
    /// looks, special looks and what the library lacks"): a drinker's face, a pyromaniac's stare, a beauty, bat wings, the
    /// conjoined twins of Conjoined Twins. In any mod's Defs (Rimfolk's starter list is <c>Defs/PortraitLookDefs</c>; the
    /// lead's catalogue adds the rest):
    /// <code>
    /// &lt;ColonyCouncil.PortraitLookDef&gt;
    ///   &lt;defName&gt;Look_Drinker&lt;/defName&gt;
    ///   &lt;word&gt;drinker&lt;/word&gt;
    ///   &lt;label&gt;a drinker's face&lt;/label&gt;
    ///   &lt;special&gt;false&lt;/special&gt;
    ///   &lt;traits&gt;&lt;li&gt;DrugDesire:2&lt;/li&gt;&lt;/traits&gt;
    ///   &lt;hediffs&gt;&lt;li&gt;AlcoholAddiction&lt;/li&gt;&lt;/hediffs&gt;
    ///   &lt;prompt&gt;a red, bulbous nose and tired, watery eyes&lt;/prompt&gt;
    /// &lt;/ColonyCouncil.PortraitLookDef&gt;
    /// </code>
    /// A person has the look when they have any of its traits (a line with a degree only at that degree), active genes,
    /// hediffs or their race's body. Every name is soft: one naming a def of a mod not loaded never matches (a note in the
    /// log in dev mode). Records name the looks they show in <c>&lt;looks&gt;</c> by the word.
    /// <list type="bullet">
    /// <item>Any look: a picture showing it stands only for people who have it (the player: "it should always be also
    /// based on an actual pawn's characteristic, and only then also this image is loaded"), <see cref="PortraitLibraryMath.LooksAgree"/>.</item>
    /// <item>An ordinary look of a person's that a picture doesn't show costs a little (<see cref="PortraitLibraryMath.LookCost"/>),
    /// so a picture showing it is preferred and a plain one still stands in.</item>
    /// <item>A special look (<see cref="special"/>): a person with it only gets a picture showing it, else the game's own
    /// drawing (the player: "rather shows no image than ... a really not fitting one").</item>
    /// </list>
    /// </summary>
    public class PortraitLookDef : Def
    {
        /// <summary>The word records use for it in <c>&lt;looks&gt;</c>, and missing.csv in its look lines.</summary>
        public string word;
        /// <summary>A special look: only a picture showing it stands for someone with it, and such a picture only for them.</summary>
        public bool special;
        /// <summary>Traits that give it: a trait's def name, or its name and a degree ("DrugDesire:2", "Beauty:-1").</summary>
        public List<string> traits = new List<string>();
        /// <summary>Genes that give it (def names), while active.</summary>
        public List<string> genes = new List<string>();
        /// <summary>Hediffs that give it (def names).</summary>
        public List<string> hediffs = new List<string>();
        /// <summary>Races' bodies that give it (the race's body def, such as Conjoined Twins' MultiMind_ConjoinedHumanBody).</summary>
        public List<string> bodies = new List<string>();
        /// <summary>Words for the image prompts (the lead's scripts read them); not read by the game.</summary>
        public string prompt;

        [Unsaved] private LookRule rule;
        [Unsaved] private List<string> problems;

        /// <summary>The look in plain words, for the matcher.</summary>
        public LookRule Rule
        {
            get
            {
                if (rule == null) Read();
                return rule;
            }
        }

        private void Read()
        {
            problems = new List<string>();
            rule = PortraitLookMath.Rule(word, special, traits, genes, hediffs, bodies, problems);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            Read();
            foreach (var p in problems) yield return p;
            if (!rule.word.NullOrEmpty())
                foreach (var other in DefDatabase<PortraitLookDef>.AllDefsListForReading)
                    if (other != this && string.Equals(other.word?.Trim(), rule.word, StringComparison.OrdinalIgnoreCase)
                                      && string.CompareOrdinal(other.defName ?? "", defName ?? "") < 0)
                        yield return "word '" + rule.word + "' is also " + other.defName + "'s; a word belongs to one look";
        }
    }

    /// <summary>
    /// The looks in the game (UI/Faces/PortraitLookDef.cs): every <see cref="PortraitLookDef"/> read into the book once,
    /// before any picture's record is read, and a pawn's looks read from their traits, active genes, hediffs and body.
    /// </summary>
    public static class PortraitLooks
    {
        private static bool built;
        private static readonly List<KeyValuePair<string, int>> traitBuf = new List<KeyValuePair<string, int>>();
        private static readonly List<string> geneBuf = new List<string>(), hediffBuf = new List<string>();

        /// <summary>The book of looks made from the defs, once (on the game's thread, before the library folder is read on a worker).</summary>
        public static void Ensure()
        {
            if (built) return;
            built = true;
            try
            {
                var defs = DefDatabase<PortraitLookDef>.AllDefsListForReading.OrderBy(d => d.defName, StringComparer.Ordinal).ToList();
                PortraitLookMath.Use(new LookBook(defs.Select(d => d.Rule)));
                if (Prefs.DevMode) NoteUnloaded(defs);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("portraits: reading the looks", e);
            }
        }

        /// <summary>In dev mode, one note of the looks' lines that name defs no loaded mod has (they never match; nothing wrong).</summary>
        private static void NoteUnloaded(List<PortraitLookDef> defs)
        {
            var missing = new List<string>();
            foreach (var d in defs)
            {
                var r = d.Rule;
                foreach (var t in r.traits)
                    if (DefDatabase<TraitDef>.GetNamedSilentFail(t.Key) == null)
                        missing.Add(r.word + ": trait " + t.Key);
                foreach (var g in r.genes)
                    if (DefDatabase<GeneDef>.GetNamedSilentFail(g) == null)
                        missing.Add(r.word + ": gene " + g);
                foreach (var h in r.hediffs)
                    if (DefDatabase<HediffDef>.GetNamedSilentFail(h) == null)
                        missing.Add(r.word + ": hediff " + h);
                foreach (var b in r.bodies)
                    if (DefDatabase<BodyDef>.GetNamedSilentFail(b) == null)
                        missing.Add(r.word + ": body " + b);
            }
            if (missing.Count > 0)
                Log.Message("[Rimfolk] Portraits: " + defs.Count + " looks; " + missing.Count + " of their lines name defs of mods not loaded, so they never match (nothing wrong): "
                            + string.Join("; ", missing.Take(20)) + (missing.Count > 20 ? "; ..." : ""));
        }

        /// <summary>A pawn's looks with what gave each (its special ones only, where <paramref name="specialOnly"/>); on the game's thread.</summary>
        public static List<LookFound> Of(Pawn p, bool specialOnly)
        {
            Ensure();
            var book = PortraitLookMath.Book;
            if (p == null || book.Count == 0 || specialOnly && !book.AnySpecial) return new List<LookFound>();
            traitBuf.Clear();
            geneBuf.Clear();
            hediffBuf.Clear();
            var traits = p.story?.traits?.allTraits;
            if (traits != null)
                foreach (var t in traits)
                    if (t?.def != null && !t.Suppressed)
                        traitBuf.Add(new KeyValuePair<string, int>(t.def.defName, t.Degree));
            var genes = p.genes?.GenesListForReading;
            if (genes != null)
                foreach (var g in genes)
                    if (g?.def != null && g.Active)
                        geneBuf.Add(g.def.defName);
            var hediffs = p.health?.hediffSet?.hediffs;
            if (hediffs != null)
                foreach (var h in hediffs)
                    if (h?.def != null)
                        hediffBuf.Add(h.def.defName);
            return book.Of(traitBuf, geneBuf, hediffBuf, p.RaceProps?.body?.defName, specialOnly);
        }

        /// <summary>A pawn's looks into the library's words.</summary>
        public static void Fill(Pawn p, PortraitLook l)
        {
            foreach (var f in Of(p, false)) l.AddLook(f.word, f.special);
        }

        /// <summary>A pawn's special looks now (read every few seconds for anyone whose picture is kept; nothing where no look is special).</summary>
        public static HashSet<string> SpecialsOf(Pawn p)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in Of(p, true)) set.Add(f.word);
            return set;
        }

        /// <summary>The mod of what gave a pawn a look (its trait, gene, hediff or body), for the missing list; "" where it isn't known.</summary>
        public static string ModOf(Pawn p, string word)
        {
            foreach (var f in Of(p, false))
            {
                if (!string.Equals(f.word, word, StringComparison.OrdinalIgnoreCase)) continue;
                Def def = f.from == "trait" ? DefDatabase<TraitDef>.GetNamedSilentFail(f.def)
                    : f.from == "gene" ? DefDatabase<GeneDef>.GetNamedSilentFail(f.def)
                    : f.from == "hediff" ? DefDatabase<HediffDef>.GetNamedSilentFail(f.def)
                    : (Def)DefDatabase<BodyDef>.GetNamedSilentFail(f.def);
                return def?.modContentPack?.PackageIdPlayerFacing ?? "";
            }
            return "";
        }
    }
}
