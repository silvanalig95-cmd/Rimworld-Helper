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

        /// <summary>Events that make this wish come up right away (a raid, a death, a cold night).</summary>
        public List<WishTrigger> triggers;
        /// <summary>Extra consequences beyond the mood memory.</summary>
        public List<WishEffect> grantEffects;
        public List<WishEffect> ignoreEffects;
        /// <summary>How much granting this stirs envy in others (0 = nobody minds, 1 = a private suite).</summary>
        public float envy = 0.4f;
        /// <summary>Greedy or jealous onlookers may ask for the same thing once they've seen it granted.</summary>
        public bool meToo;
        /// <summary>Chance that someone speaks up against it when it's voiced (if anyone has a reason to).</summary>
        public float objectionChance = 0.35f;
        /// <summary>The deadline isn't shortened by low trust or high standards (long wishes like getting clean or a rescue).</summary>
        public bool fixedPatience;
        /// <summary>Colonists with these traits object to it (a kind soul against an execution).</summary>
        public List<TraitDef> objectorTraits;
        /// <summary>+1: whoever is fond of the wish's other pawn objects; -1: whoever can't stand them.</summary>
        public int objectByOpinionOfOther;
        /// <summary>Objection lines for this wish ({PAWN} objects to {OTHER}'s wish about {ABOUT}); falls back to the general pool.</summary>
        public FlavorPoolDef objectionPool;

        /// <summary>For the XML-driven workers (WishWorker_Have, _Stock, _Wear...): what is wanted and by whom.</summary>
        public WishParams wants;
        /// <summary>
        /// Beliefs that rule this wish out (Wishes/Beliefs.cs): a smokehouse for meat isn't asked for by a vegetarian,
        /// a party by one whose faith frowns on fun. Never raised for someone who holds one, and let go quietly if they
        /// come to hold one while it's open.
        /// </summary>
        public List<Belief> forbiddenBy;

        [Unsaved] private ulong forbiddenMask;
        [Unsaved] private bool forbiddenMaskSet;

        /// <summary><see cref="forbiddenBy"/> as one mask.</summary>
        public ulong ForbiddenMask
        {
            get
            {
                if (!forbiddenMaskSet)
                {
                    forbiddenMask = BeliefMath.Mask(forbiddenBy);
                    forbiddenMaskSet = true;
                }
                return forbiddenMask;
            }
        }
        /// <summary>How granting it shapes the colony's character (an execution, a party, letting a prisoner go).</summary>
        public WayShift ways;

        // ---- Promises kept (Wishes/Kept.cs) ----
        /// <summary>
        /// Something that stays and can be taken away again (a bed for guests, a room of one's own, a telescope, an
        /// assignment): once granted, it's watched for <see cref="watchDays"/>, and taking it away upsets the one who asked.
        /// One-offs (a party, a hunt, a visit) aren't.
        /// </summary>
        public bool lasting;
        /// <summary>How long a granted lasting wish is watched (rooms longer); after that, changing it is just life.</summary>
        public float watchDays = KeptMath.DefaultWatchDays;
        /// <summary>Stocks that are meant to be used (medicine, healroot) or spent (silver): the share of the amount that must stay.</summary>
        public float keepShare = 1f;
        /// <summary>
        /// What's gone, for the message when it goes missing ({PAWN}, {OTHER}, {THING}, {THINGBARE}, {SUBJECT}, {WISH}):
        /// "{PAWN_nameDef}'s guest bed is no longer kept for guests." Without it, the worker's own line or a general one.
        /// </summary>
        [MustTranslate] public string lostText;

        public ToneLines request = new ToneLines();
        public ToneLines requestForOther = new ToneLines();
        public ToneLines thanks = new ToneLines();
        public ToneLines expired = new ToneLines();
        /// <summary>Said when bringing the wish up again; falls back to the shared reminder pool.</summary>
        public ToneLines reminder = new ToneLines();

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
            if (lasting && watchDays <= 0f) yield return "a lasting wish needs watchDays above 0";
            if (keepShare <= 0f || keepShare > 1f) yield return "keepShare must be above 0 and at most 1";
        }
    }
}
