using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One rule that lets a mood thought feed a feeling. A thought counts with the size of its mood (as the game adds it
    /// up), times <see cref="weight"/>, when every condition holds.
    /// </summary>
    public class FeelingFeed
    {
        /// <summary>
        /// The thought's defName, or a pattern with one "*" ("Council_Soc_*", "My*Died"). Empty: any thought (a generic
        /// rule, like "a bad memory about someone they dislike").
        /// </summary>
        public string thought;
        /// <summary>-1: only while it hurts; 1: only while it helps; 0: either. Default: the feeling's own side.</summary>
        public int sign = int.MinValue;
        /// <summary>Only a thought about another person (a memory or social thought with someone in it).</summary>
        public bool aboutSomeone;
        /// <summary>Only while their opinion of that person is below / above this.</summary>
        public int opinionBelow = int.MaxValue;
        public int opinionAbove = int.MinValue;
        public float weight = 1f;

        [Unsaved] private string prefix, suffix;
        [Unsaved] private bool parsed;

        /// <summary>Whether this rule's name or pattern matches the defName (true for a generic rule).</summary>
        public bool NameMatches(string defName)
        {
            if (thought.NullOrEmpty()) return true;
            if (!parsed)
            {
                parsed = true;
                int star = thought.IndexOf('*');
                if (star >= 0)
                {
                    prefix = thought.Substring(0, star);
                    suffix = thought.Substring(star + 1);
                }
            }
            if (prefix == null) return defName == thought;
            return defName.Length >= prefix.Length + suffix.Length && defName.StartsWith(prefix, StringComparison.Ordinal)
                   && defName.EndsWith(suffix, StringComparison.Ordinal);
        }

        public bool Generic => thought.NullOrEmpty();

        public bool Exact => !thought.NullOrEmpty() && thought.IndexOf('*') < 0;
    }

    /// <summary>A trait that makes a feeling stronger or weaker (by defName, so mods' traits can be named).</summary>
    public class FeelingTraitFactor
    {
        public string trait;
        /// <summary>Only at this degree (Nerves -1, NaturalMood 2...); leave out for any.</summary>
        public int degree = int.MinValue;
        public float factor = 1f;
    }

    /// <summary>How a feeling colors one kind of dream.</summary>
    public class DreamKindFactor
    {
        public DreamKind kind;
        public float factor = 1f;
    }

    /// <summary>
    /// Put on a ThoughtDef (any mod's, through a patch, or Rimfolk's own) to say which feelings it feeds, without
    /// touching the feelings themselves: <c>&lt;li Class="ColonyCouncil.HeartTags"&gt;&lt;feelings&gt;&lt;li&gt;Anger&lt;/li&gt;&lt;/feelings&gt;&lt;/li&gt;</c>.
    /// Names are a feeling's key or defName.
    /// </summary>
    public class HeartTags : DefModExtension
    {
        public List<string> feelings;
        public float weight = 1f;
    }

    /// <summary>
    /// A feeling: short-term color that sits on top of mood without adding to it. It's fed by the mood thoughts it lists
    /// (and by what its worker sees), and it nudges what the colonist wishes for, how they get on with others, what
    /// they dream and how fast strain builds or drains. Its name and description are in the Keyed file,
    /// under CC_Heart_F_{key}, CC_Heart_F_{key}_Adj, CC_Heart_F_{key}_Desc and CC_Heart_F_{key}_Does.
    /// </summary>
    public class FeelingDef : Def
    {
        /// <summary>Short name for keys and tags: Grief, Fear, Joy...</summary>
        public string key;
        /// <summary>A bright feeling (joy, pride) rather than a hard one (grief, fear).</summary>
        public bool bright;
        public Color color = Color.white;
        public int order;
        /// <summary>Thought defNames (or patterns with one "*") that feed it, on its own side of mood, at full weight.</summary>
        public List<string> thoughts;
        /// <summary>Rules with conditions and weights.</summary>
        public List<FeelingFeed> feeds;
        /// <summary>Computes what thoughts can't say (a raid on the map, a new arrival, quiet days).</summary>
        public Type workerClass;
        /// <summary>Intensity from which it counts as felt.</summary>
        public float threshold = 6f;
        /// <summary>Traits that never feel it (defNames; "Nerves:2" for a degree).</summary>
        public List<string> immuneTraits;
        public List<FeelingTraitFactor> traitFactors;
        /// <summary>Strain deepens it: intensity += strain × this, once it's there at all.</summary>
        public float fromStrain;
        /// <summary>A feeling it eases: that one's intensity drops by this one's × <see cref="counterAmount"/>.</summary>
        public FeelingDef counters;
        public float counterAmount = 0.5f;

        // ---------------- What it does (each at full intensity; less when weaker) ----------------

        /// <summary>Strain it adds per hour (the source of the same name, capped per day).</summary>
        public float strainPerHour;
        /// <summary>Which strain source it feeds.</summary>
        public StrainSource strainSource = StrainSource.Fear;
        /// <summary>Factor on how fast strain drains (joy 1.25, despair 0.8).</summary>
        public float relief = 1f;
        /// <summary>Wishes it makes likelier, and by how much at most.</summary>
        public List<WishDef> wishes;
        public List<WishCategory> wishCategories;
        public float wishFactor = 1.4f;
        /// <summary>Factor on how patient they are with a wish (frustration: less).</summary>
        public float patience = 1f;
        /// <summary>Factors on moments between colonists they start: warm ones, frictions, and grief shared.</summary>
        public float warm = 1f;
        public float friction = 1f;
        public float griefSharing = 1f;
        /// <summary>Extra factor on moments with the person it's about (the one they're angry at, grateful to).</summary>
        public float towardTarget = 1f;
        /// <summary>Whether <see cref="towardTarget"/> applies to frictions (anger, envy) or to warm moments (gratitude).</summary>
        public bool targetFriction;
        public List<DreamKindFactor> dreams;
        /// <summary>Factor on the chance of an episode.</summary>
        public float episode = 1f;
        /// <summary>Daily chance of an inspiration at full intensity (rare and capped: once a month at most).</summary>
        public float inspirationPerDay;
        /// <summary>Trust in the council gained every three days while it lasts, when the council is among what fed it.</summary>
        public float trust;

        [Unsaved] private FeelingWorker workerInt;
        [Unsaved] private List<FeelingFeed> allFeeds;

        public FeelingWorker Worker
        {
            get
            {
                if (workerInt == null && workerClass != null)
                {
                    workerInt = (FeelingWorker)Activator.CreateInstance(workerClass);
                    workerInt.def = this;
                }
                return workerInt;
            }
        }

        /// <summary>Every rule: the plain list as rules on its own side, then the detailed ones.</summary>
        public List<FeelingFeed> AllFeeds
        {
            get
            {
                if (allFeeds != null) return allFeeds;
                allFeeds = new List<FeelingFeed>();
                if (thoughts != null)
                    foreach (var t in thoughts)
                        if (!t.NullOrEmpty()) allFeeds.Add(new FeelingFeed { thought = t, sign = DefaultSign });
                if (feeds != null)
                    foreach (var f in feeds)
                    {
                        if (f == null) continue;
                        if (f.sign == int.MinValue) f.sign = DefaultSign;
                        allFeeds.Add(f);
                    }
                return allFeeds;
            }
        }

        public int DefaultSign => bright ? 1 : -1;

        public string Label => Keyed("", label);

        public string Adjective => Keyed("_Adj", label);

        public string Explanation => Keyed("_Desc", description);

        public string Does => Keyed("_Does", null);

        private string Keyed(string suffix, string fallback)
        {
            string k = "CC_Heart_F_" + key + suffix;
            return k.CanTranslate() ? k.Translate().ToString() : fallback ?? "";
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (key.NullOrEmpty()) yield return "feeling has no key";
            if (workerClass != null && !typeof(FeelingWorker).IsAssignableFrom(workerClass)) yield return "workerClass must be a FeelingWorker";
            if (thoughts.NullOrEmpty() && feeds.NullOrEmpty() && workerClass == null) yield return "feeling has nothing that feeds it";
        }
    }

    /// <summary>What a feeling's worker adds to it this hour, with a reason the player can read.</summary>
    public interface IFeelingSink
    {
        void Add(float amount, string cause, Pawn about = null);
    }

    /// <summary>The part of a feeling that thoughts can't say: computed from the colonist and their colony.</summary>
    public class FeelingWorker
    {
        public FeelingDef def;

        public virtual void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
        }
    }

    /// <summary>The four long-term needs of the heart.</summary>
    public enum HeartNeed
    {
        Belonging,
        Purpose,
        Safety,
        Freedom
    }

    /// <summary>
    /// What follows from a need of the heart being unmet: which wishes and life goals become a little likelier for
    /// someone who lacks it. The values themselves are computed (HeartNeeds.cs) and never saved.
    /// </summary>
    public class HeartNeedDef : Def
    {
        public HeartNeed need;
        public List<WishDef> wishes;
        public List<WishCategory> wishCategories;
        public List<AspirationDef> goals;

        public string Label => ("CC_Heart_Need_" + need).Translate();
    }
}
