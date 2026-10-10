using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    public enum WishCategory
    {
        Comfort,
        Work,
        Social,
        Health,
        Gear,
        Safety,
        Status,
        Childhood
    }

    /// <summary>
    /// One kind of wish a colonist can have. The worker decides when it makes sense and when it is
    /// fulfilled; the def holds the flavour text and the reward.
    /// </summary>
    public class WishDef : Def
    {
        public Type workerClass;
        public WishCategory category = WishCategory.Comfort;

        /// <summary>Relative chance of this wish being picked when it is possible at all.</summary>
        public float weight = 1f;
        public float expiryDays = 10f;
        /// <summary>After a wish resolves, the same colonist won't make it again for this long.</summary>
        public float cooldownDays = 15f;

        /// <summary>Memory the colonist gains when the wish comes true.</summary>
        public ThoughtDef grantedThought;
        public float inspirationChance = 0f;

        /// <summary>Several colonists can back the same wish (e.g. a rec room).</summary>
        public bool allowPetition = false;
        /// <summary>Kind colonists can make this wish on someone else's behalf.</summary>
        public bool allowForOthers = false;
        /// <summary>Luxury wishes: ascetics rarely make them.</summary>
        public bool luxury = false;

        /// <summary>A room template the player can plan straight from the wish.</summary>
        public RoomTemplateDef template;
        /// <summary>Social interaction a colonist uses to grumble or chat about this wish.</summary>
        public InteractionDef talkInteraction;

        [MustTranslate] public string fulfilledWhen;

        public ToneLines request = new ToneLines();
        public ToneLines requestForOther = new ToneLines();
        public ToneLines thanks = new ToneLines();
        public ToneLines expired = new ToneLines();

        [Unsaved] private WishWorker workerInt;

        public WishWorker Worker
        {
            get
            {
                if (workerInt == null)
                {
                    workerInt = (WishWorker)Activator.CreateInstance(workerClass);
                    workerInt.def = this;
                }
                return workerInt;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (workerClass == null) yield return "workerClass is null";
            else if (!typeof(WishWorker).IsAssignableFrom(workerClass)) yield return "workerClass is not a WishWorker";
            if (request == null || !request.Any) yield return "no request lines";
        }
    }
}
