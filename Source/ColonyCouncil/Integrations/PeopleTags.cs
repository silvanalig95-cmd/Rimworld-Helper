using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A tag for a kind of person that other mods add, so Rimfolk's XML can name them without naming the mod: "mage"
    /// for A RimWorld of Magic's spellcasting classes, "fighter" for its combat classes, "appointed" for beliefs that
    /// put a ruler above the vote. Everything is matched by name when the game runs, so a group whose mod is missing
    /// simply never fits anyone. A person fits when any one of the lists fits them.
    /// </summary>
    public class TagGroupDef : Def
    {
        /// <summary>The tag they get (one word: no commas, bars, brackets or "!").</summary>
        public string tag;
        /// <summary>Trait defNames, any degree.</summary>
        public List<string> traits;
        /// <summary>Meme defNames of their own ideoligion (Ideology).</summary>
        public List<string> memes;
        /// <summary>Xenotype defNames (Biotech).</summary>
        public List<string> xenotypes;
        /// <summary>Active gene defNames (Biotech).</summary>
        public List<string> genes;
        /// <summary>Hediff defNames.</summary>
        public List<string> hediffs;
        /// <summary>
        /// Optional voice for people it fits (Plain, Warm, Blunt, Cheerful, Gloomy, Cold), used after the vanilla
        /// traits that set one (Psychopath, Abrasive, Kind, the mood traits). Lower <see cref="order"/> wins.
        /// </summary>
        public string tone;
        public int order;

        [Unsaved] private HashSet<string> traitSet, memeSet, xenoSet, geneSet, hediffSet;
        [Unsaved] private Tone? parsedTone;
        [Unsaved] private bool resolved;

        public Tone? Voice
        {
            get
            {
                Resolve();
                return parsedTone;
            }
        }

        private void Resolve()
        {
            if (resolved) return;
            resolved = true;
            traitSet = Set(traits);
            memeSet = Set(memes);
            xenoSet = Set(xenotypes);
            geneSet = Set(genes);
            hediffSet = Set(hediffs);
            if (!tone.NullOrEmpty() && System.Enum.TryParse(tone, out Tone t)) parsedTone = t;
        }

        private static HashSet<string> Set(List<string> names) => names.NullOrEmpty() ? null : new HashSet<string>(names);

        public bool Fits(Pawn p)
        {
            Resolve();
            if (p == null) return false;
            if (traitSet != null && p.story?.traits != null)
                foreach (var t in p.story.traits.allTraits)
                    if (t?.def != null && traitSet.Contains(t.def.defName)) return true;
            if (memeSet != null && ModsConfig.IdeologyActive && p.Ideo?.memes != null)
                foreach (var m in p.Ideo.memes)
                    if (m != null && memeSet.Contains(m.defName)) return true;
            if ((xenoSet != null || geneSet != null) && ModsConfig.BiotechActive && p.genes != null)
            {
                if (xenoSet != null && p.genes.Xenotype != null && xenoSet.Contains(p.genes.Xenotype.defName)) return true;
                if (geneSet != null)
                    foreach (var g in p.genes.GenesListForReading)
                        if (g?.def != null && g.Active && geneSet.Contains(g.def.defName)) return true;
            }
            if (hediffSet != null && p.health?.hediffSet != null)
                foreach (var h in p.health.hediffSet.hediffs)
                    if (h?.def != null && hediffSet.Contains(h.def.defName)) return true;
            return false;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (tag.NullOrEmpty()) yield return "tag group has no tag";
            else if (tag.IndexOfAny(new[] { ',', '|', '[', ']', '!', ' ' }) >= 0) yield return "tag must be one word without , | [ ] ! or spaces";
            if (!tone.NullOrEmpty() && !System.Enum.TryParse(tone, out Tone _)) yield return "unknown tone " + tone;
        }
    }

    /// <summary>
    /// Who someone is, as tags (see <see cref="Flavor"/>): their traits (trait:Kind, trait:Priest, degree names like
    /// trait:Industrious), passions (skill:Cooking), their ideoligion's memes (meme:VME_Royal), their xenotype
    /// (xeno:Hussar), the <see cref="TagGroupDef"/> groups they belong to (mage, fighter...), and what their body asks
    /// of them: nosleep (no need to sleep), nofood (no need to eat), hemogenic, deathrest, psycaster. Flavor lines,
    /// decree reactions, office preferences, dilemma roles, dreams and life goals can all ask for these by name, so a
    /// mod's trait, class or meme counts when the mod is there and costs nothing when it isn't.
    /// </summary>
    public static class PeopleTags
    {
        // Who someone is changes rarely (a trait gained, a conversion): worked out at most once an in-game hour.
        private static readonly Dictionary<int, (int tick, HashSet<string> tags)> cache = new Dictionary<int, (int, HashSet<string>)>();
        private const int CacheTicks = 2500;
        private static readonly Dictionary<string, (string tag, bool neg)[][]> parsed = new Dictionary<string, (string, bool)[][]>();
        private static List<TagGroupDef> groups;
        private static List<TagGroupDef> toneGroups;

        private static List<TagGroupDef> Groups => groups ??= DefDatabase<TagGroupDef>.AllDefsListForReading.ToList();

        /// <summary>Forget the last game's people (a new game or a load).</summary>
        public static void Reset() => cache.Clear();

        /// <summary>Who this person is, as tags. Never null.</summary>
        public static HashSet<string> Identity(Pawn p)
        {
            if (p == null) return new HashSet<string>();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (cache.TryGetValue(p.thingIDNumber, out var c) && now >= c.tick && now - c.tick < CacheTicks) return c.tags;
            if (cache.Count > 600) cache.Clear();
            var tags = new HashSet<string>();
            try
            {
                Build(p, tags);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("people tags", e);
            }
            cache[p.thingIDNumber] = (now, tags);
            return tags;
        }

        private static void Build(Pawn p, HashSet<string> tags)
        {
            if (p.story?.traits != null)
            {
                foreach (var t in p.story.traits.allTraits)
                {
                    if (t?.def == null) continue;
                    tags.Add("trait:" + t.def.defName);
                    if (t.def.degreeDatas != null && t.def.degreeDatas.Count > 1)
                    {
                        string name = Flavor.DegreeTag(t.CurrentData);
                        if (name != null) tags.Add("trait:" + name);
                    }
                }
            }
            if (p.skills != null)
                foreach (var s in p.skills.skills)
                    if (!s.TotallyDisabled && People.Passionate(s)) tags.Add("skill:" + s.def.defName);
            AddExtras(p, tags);
        }

        /// <summary>Memes, xenotype, what their body needs, and the tag groups that fit them.</summary>
        private static void AddExtras(Pawn p, HashSet<string> tags)
        {
            if (ModsConfig.IdeologyActive && p.Ideo?.memes != null)
                foreach (var m in p.Ideo.memes)
                    if (m != null) tags.Add("meme:" + m.defName);
            if (ModsConfig.BiotechActive && p.genes != null)
            {
                if (p.genes.Xenotype != null) tags.Add("xeno:" + p.genes.Xenotype.defName);
                if (p.genes.GetFirstGeneOfType<Gene_Hemogen>() != null) tags.Add("hemogenic");
                if (p.genes.GetFirstGeneOfType<Gene_Deathrest>() != null) tags.Add("deathrest");
            }
            // Needs only say something about people the game keeps a mood for (our own, and those we hold).
            if (p.needs?.mood != null)
            {
                if (p.needs.rest == null) tags.Add("nosleep");
                if (p.needs.food == null) tags.Add("nofood");
            }
            if (ModsConfig.RoyaltyActive && p.HasPsylink) tags.Add("psycaster");
            // Rimfolk's quirks, like traits: quirk:Hums, quirk:SleepTalker (Interplay/).
            Interplay.AddQuirkTags(p, tags);
            foreach (var g in Groups)
            {
                if (g.tag.NullOrEmpty() || tags.Contains(g.tag)) continue;
                try
                {
                    if (g.Fits(p)) tags.Add(g.tag);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("tag group " + g.defName, e);
                }
            }
        }

        /// <summary>Adds who someone is to the moment's flavor tags (memes, xenotype, groups, needs).</summary>
        public static void AddTo(Pawn p, HashSet<string> tags)
        {
            if (p == null || tags == null) return;
            tags.UnionWith(Identity(p));
        }

        /// <summary>Whether they have this one tag.</summary>
        public static bool Has(Pawn p, string tag) => p != null && !tag.NullOrEmpty() && Identity(p).Contains(tag);

        /// <summary>
        /// Whether a tag expression fits them, written like a flavor line's tags without the brackets:
        /// "trait:VTE_Workaholic", "mage|fighter", "meme:Collectivist,!trait:Lazy". Empty fits nobody.
        /// </summary>
        public static bool Matches(Pawn p, string expr)
        {
            if (p == null || expr.NullOrEmpty()) return false;
            return Matches(Parse(expr), Identity(p));
        }

        /// <summary>The same rules as <see cref="Flavor.Match"/>: every comma part must fit, by any of its | alternatives.</summary>
        private static bool Matches((string tag, bool neg)[][] parts, HashSet<string> tags)
        {
            if (parts.Length == 0) return false;
            foreach (var alts in parts)
            {
                bool ok = false;
                foreach (var (tag, neg) in alts)
                {
                    if (tags.Contains(tag) != neg)
                    {
                        ok = true;
                        break;
                    }
                }
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>"a|b,!c" as [[a, b], [not c]], worked out once per expression.</summary>
        private static (string tag, bool neg)[][] Parse(string expr)
        {
            if (parsed.TryGetValue(expr, out var parts)) return parts;
            parts = expr.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0)
                .Select(t => t.Split('|').Select(a => a.Trim()).Where(a => a.Length > 0)
                    .Select(a => a.StartsWith("!") ? (a.Substring(1), true) : (a, false)).ToArray())
                .Where(alts => alts.Length > 0).ToArray();
            if (parsed.Count < 4000) parsed[expr] = parts;
            return parts;
        }

        /// <summary>The voice a tag group gives them (a mod's trait or class), or null.</summary>
        public static Tone? ToneOf(Pawn p)
        {
            if (p == null) return null;
            toneGroups ??= Groups.Where(g => g.Voice != null).OrderBy(g => g.order).ToList();
            if (toneGroups.Count == 0) return null;
            var tags = Identity(p);
            foreach (var g in toneGroups)
                if (tags.Contains(g.tag)) return g.Voice;
            return null;
        }
    }
}
