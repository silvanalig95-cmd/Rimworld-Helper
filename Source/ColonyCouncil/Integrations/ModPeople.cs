using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>How people of a kind (a tag expression, see <see cref="PeopleTags"/>) feel about a decree.</summary>
    public class DecreeLeaning
    {
        public DecreeDef decree;
        /// <summary>Tag expression: "trait:VTE_Workaholic", "fighter", "meme:Collectivist".</summary>
        public string who;
        public DecreeReaction reaction = DecreeReaction.Neutral;
        /// <summary>
        /// Checked before the decree's own reactions (a personal trait that matters more than anything else), rather
        /// than after them, where it only decides for people the decree's own list says nothing about (a belief).
        /// </summary>
        public bool first;
    }

    /// <summary>Who the colony would rather see in a seat, and who not (on top of the office's skills and traits).</summary>
    public class OfficeLeaning
    {
        public OfficeDef office;
        /// <summary>Tag expressions; each one that fits counts like one of the office's good (or bad) traits.</summary>
        public List<string> good;
        public List<string> bad;
    }

    /// <summary>How people of a kind feel about the colony's ways (each way -1.5..1.5, like a trait's).</summary>
    public class CharacterLeaning
    {
        public string who;
        public WayShift affinity;
    }

    /// <summary>
    /// Who else wants a data-driven wish (WishWorker_Have, _Stock, _Wear...), and who wants it more. Merged into the
    /// wish's <see cref="WishParams"/> at startup.
    /// </summary>
    public class WishLeaning
    {
        public WishDef wish;
        /// <summary>Also wanted by anyone this fits, whatever the wish's own trait and passion requirements say.</summary>
        public string alsoWho;
        /// <summary>Why, for them ({PAWN}); the wish's own reason when empty.</summary>
        [MustTranslate] public string alsoReason;
        /// <summary>Wanted more often by anyone this fits.</summary>
        public string favoredBy;
        public float favorFactor = 2.5f;
    }

    /// <summary>Wishes that only make sense for someone with this need (beds for people who sleep, meals for people who eat).</summary>
    public class NeedGate
    {
        public NeedDef need;
        public List<WishDef> wishes;
    }

    /// <summary>Wishes that make no sense for people of a kind (meals for someone who lives on sunlight).</summary>
    public class WishBlock
    {
        public string who;
        public List<WishDef> wishes;
    }

    /// <summary>
    /// What Rimfolk knows about the people other mods add, in XML: how a mod's traits, classes and memes lean on
    /// decrees, offices and the colony's ways, who else wants a wish, and which wishes need a body that sleeps or
    /// eats. Everything names people by tags (see <see cref="PeopleTags"/>), which are matched by name while the game
    /// runs, so with the mod missing an entry simply never fits anyone.
    /// </summary>
    public class ModPeopleDef : Def
    {
        public List<DecreeLeaning> decrees;
        public List<OfficeLeaning> offices;
        public List<CharacterLeaning> character;
        public List<WishLeaning> wishes;
        public List<NeedGate> needs;
        public List<WishBlock> notFor;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (decrees != null)
                foreach (var d in decrees)
                    if (d.decree == null || d.who.NullOrEmpty()) yield return "decree leaning needs a decree and who";
            if (offices != null)
                foreach (var o in offices)
                    if (o.office == null) yield return "office leaning needs an office";
            if (character != null)
                foreach (var c in character)
                    if (c.who.NullOrEmpty() || c.affinity == null) yield return "character leaning needs who and affinity";
            if (wishes != null)
                foreach (var w in wishes)
                {
                    if (w.wish == null) yield return "wish leaning needs a wish";
                    else if (w.alsoWho.NullOrEmpty() && w.favoredBy.NullOrEmpty()) yield return "wish leaning for " + w.wish.defName + " needs alsoWho or favoredBy";
                }
            if (needs != null)
                foreach (var n in needs)
                    if (n.need == null || n.wishes.NullOrEmpty()) yield return "need gate needs a need and wishes";
            if (notFor != null)
                foreach (var b in notFor)
                    if (b.who.NullOrEmpty() || b.wishes.NullOrEmpty()) yield return "wish block needs who and wishes";
        }
    }

    /// <summary>The <see cref="ModPeopleDef"/> rules, gathered once, for the places that ask.</summary>
    public static class ModPeople
    {
        private static Dictionary<DecreeDef, List<DecreeLeaning>> decreesFirst, decreesAfter;
        private static Dictionary<OfficeDef, List<OfficeLeaning>> offices;
        private static List<CharacterLeaning> character;
        private static Dictionary<WishDef, List<NeedDef>> needGates;
        private static Dictionary<WishDef, List<string>> wishBlocks;

        private static IEnumerable<ModPeopleDef> Defs => DefDatabase<ModPeopleDef>.AllDefsListForReading;

        private static void Build()
        {
            if (decreesFirst != null) return;
            decreesFirst = new Dictionary<DecreeDef, List<DecreeLeaning>>();
            decreesAfter = new Dictionary<DecreeDef, List<DecreeLeaning>>();
            offices = new Dictionary<OfficeDef, List<OfficeLeaning>>();
            character = new List<CharacterLeaning>();
            needGates = new Dictionary<WishDef, List<NeedDef>>();
            wishBlocks = new Dictionary<WishDef, List<string>>();
            foreach (var def in Defs)
            {
                if (def.decrees != null)
                    foreach (var d in def.decrees.Where(d => d.decree != null && !d.who.NullOrEmpty()))
                    {
                        var map = d.first ? decreesFirst : decreesAfter;
                        if (!map.TryGetValue(d.decree, out var list)) map[d.decree] = list = new List<DecreeLeaning>();
                        list.Add(d);
                    }
                if (def.offices != null)
                    foreach (var o in def.offices.Where(o => o.office != null))
                    {
                        if (!offices.TryGetValue(o.office, out var list)) offices[o.office] = list = new List<OfficeLeaning>();
                        list.Add(o);
                    }
                if (def.character != null) character.AddRange(def.character.Where(c => !c.who.NullOrEmpty() && c.affinity != null));
                if (def.needs != null)
                    foreach (var n in def.needs.Where(n => n.need != null && n.wishes != null))
                        foreach (var w in n.wishes.Where(w => w != null))
                        {
                            if (!needGates.TryGetValue(w, out var list)) needGates[w] = list = new List<NeedDef>();
                            if (!list.Contains(n.need)) list.Add(n.need);
                        }
                if (def.notFor != null)
                    foreach (var b in def.notFor.Where(b => !b.who.NullOrEmpty() && b.wishes != null))
                        foreach (var w in b.wishes.Where(w => w != null))
                        {
                            if (!wishBlocks.TryGetValue(w, out var list)) wishBlocks[w] = list = new List<string>();
                            list.Add(b.who);
                        }
            }
        }

        private static DecreeReaction? Leaning(Dictionary<DecreeDef, List<DecreeLeaning>> map, DecreeDef decree, Pawn p)
        {
            Build();
            if (decree == null || p == null || !map.TryGetValue(decree, out var list)) return null;
            foreach (var d in list)
                if (PeopleTags.Matches(p, d.who)) return d.reaction;
            return null;
        }

        /// <summary>A reaction that comes before the decree's own list (a mod's personal trait), or null.</summary>
        public static DecreeReaction? DecreeFirst(DecreeDef decree, Pawn p) => Leaning(decreesFirst, decree, p);

        /// <summary>A reaction for someone the decree's own list says nothing about (a belief, a class), or null.</summary>
        public static DecreeReaction? DecreeAfter(DecreeDef decree, Pawn p) => Leaning(decreesAfter, decree, p);

        /// <summary>Extra standing for a seat: +2 for each good tag that fits, -3 for each bad one, like traits.</summary>
        public static float OfficeBonus(OfficeDef office, Pawn p)
        {
            Build();
            if (office == null || p == null || !offices.TryGetValue(office, out var list)) return 0f;
            float b = 0f;
            foreach (var o in list)
            {
                if (o.good != null) b += o.good.Count(t => PeopleTags.Matches(p, t)) * 2f;
                if (o.bad != null) b -= o.bad.Count(t => PeopleTags.Matches(p, t)) * 3f;
            }
            return b;
        }

        // Asked for every colonist and every way whenever their mood is worked out: kept for an in-game hour.
        private static readonly Dictionary<int, (int tick, float[] ways)> wayCache = new Dictionary<int, (int, float[])>();

        /// <summary>How much more (or less) they care for one of the colony's ways, from a mod's trait, class or meme.</summary>
        public static float WayAffinity(Pawn p, Way w)
        {
            Build();
            if (p == null || character.Count == 0) return 0f;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (!wayCache.TryGetValue(p.thingIDNumber, out var c) || now < c.tick || now - c.tick >= 2500)
            {
                if (wayCache.Count > 600) wayCache.Clear();
                var ways = new float[CharacterTracker.All.Length];
                foreach (var l in character)
                {
                    if (!PeopleTags.Matches(p, l.who)) continue;
                    foreach (var way in CharacterTracker.All) ways[(int)way] += l.affinity.Get(way);
                }
                c = (now, ways);
                wayCache[p.thingIDNumber] = c;
            }
            int i = (int)w;
            return i >= 0 && i < c.ways.Length ? c.ways[i] : 0f;
        }

        /// <summary>Forget the last game's people.</summary>
        public static void Reset() => wayCache.Clear();

        /// <summary>False for a wish that needs a body that sleeps (or eats) when theirs doesn't, or makes no sense for their kind.</summary>
        public static bool WishFits(WishDef def, Pawn p)
        {
            Build();
            if (def == null || p == null) return true;
            if (wishBlocks.TryGetValue(def, out var whos))
                foreach (var who in whos)
                    if (PeopleTags.Matches(p, who)) return false;
            // The game only gives needs to those it keeps a mood for; for anyone else, there's nothing to read.
            if (p.needs?.mood == null || !needGates.TryGetValue(def, out var needs)) return true;
            foreach (var n in needs)
                if (p.needs.TryGetNeed(n) == null) return false;
            return true;
        }

        /// <summary>Merges the wish leanings into their wishes (once, after the defs load).</summary>
        public static void ApplyWishLeanings()
        {
            int merged = 0;
            foreach (var def in Defs)
            {
                if (def.wishes == null) continue;
                foreach (var w in def.wishes)
                {
                    if (w?.wish == null) continue;
                    var p = w.wish.wants ?? (w.wish.wants = new WishParams());
                    if (!w.alsoWho.NullOrEmpty())
                    {
                        p.alsoWho = p.alsoWho.NullOrEmpty() ? w.alsoWho : p.alsoWho + "|" + w.alsoWho;
                        if (!w.alsoReason.NullOrEmpty()) p.alsoReason = w.alsoReason;
                    }
                    if (!w.favoredBy.NullOrEmpty())
                    {
                        p.favoredBy = p.favoredBy.NullOrEmpty() ? w.favoredBy : p.favoredBy + "|" + w.favoredBy;
                        p.favorFactor = Mathf.Max(p.favorFactor, w.favorFactor);
                    }
                    merged++;
                }
            }
            if (merged > 0 && Prefs.DevMode) Log.Message("[Rimfolk] merged " + merged + " wish leaning(s) for other mods' people.");
        }
    }

    /// <summary>Runs once after all defs are loaded: the other mods' people integrations that change defs.</summary>
    [StaticConstructorOnStartup]
    public static class PeopleModsStartup
    {
        static PeopleModsStartup()
        {
            try
            {
                ModPeople.ApplyWishLeanings();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("wish leanings", e);
            }
            try
            {
                ModOverlaps.Apply();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("mod overlaps", e);
            }
        }
    }
}
