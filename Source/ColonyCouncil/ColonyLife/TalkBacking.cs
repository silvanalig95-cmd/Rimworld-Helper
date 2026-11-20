using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A13: a wish talked up finds backers (the content update, the player's go of 20 November 2026). Colonists already bring
    /// up their open wish in conversation (InteractionWorker_TalkAboutWish, the game's own talk in the social log). For a
    /// wish many may back for one thing (a rec room, a sick bay, a council meeting: WishDef.allowPetition), a listener who
    /// would want the same joins it there and then, with a line each over both heads: the co-signing the wish roll already
    /// uses, its name added, the petition's deadline two days on. A petition grows this way once a day at most
    /// (TalkBackMath.MayGrow); the roll's own co-signing is unchanged.
    /// <para>
    /// The same rule as the meeting's "any other business" (A6, Ambitions/MeetingBusiness.cs): who would want the same is
    /// <see cref="WantsSame"/>'s, and joining is the roll's own <see cref="CoSign"/> rule, here with a line each.
    /// </para>
    /// </summary>
    public partial class CouncilGameComponent
    {
        private static readonly Color SpeechBacking = new Color(1f, 0.92f, 0.6f);

        /// <summary>
        /// Whether <paramref name="p"/> would want what <paramref name="w"/> asks for, and may join it in conversation: a
        /// wish many back for one thing, open, in their own colony, and one they want the same of (<see cref="WantsSame"/>,
        /// the meeting's own rule: the wish's own rule would raise it for them now, not on cooldown, not theirs already).
        /// </summary>
        public bool WouldBack(Wish w, Pawn p)
        {
            if (w?.def == null || p == null || w.state != WishState.Open || !w.def.allowPetition || !Settings.petitions) return false;
            if (w.pawn == null || !CanVoiceWishes(p) || ColonyState.Of(p) != ColonyState.Of(w.pawn)) return false;
            return WantsSame(w, p);
        }

        /// <summary>
        /// <paramref name="teller"/> talked about their open wish with <paramref name="listener"/> (the game's own conversation):
        /// a listener who would want the same may join it there and then, once a day at most for the petition.
        /// </summary>
        internal void TalkedUp(Pawn teller, Pawn listener)
        {
            if (!Settings.enableWishes || teller == null || listener == null || !listener.IsColonist) return;
            var w = OpenWishBy(teller);
            if (w == null || !WouldBack(w, listener)) return;
            int today = GenDate.DaysPassed;
            if (!TalkBackMath.MayGrow(today, w.talkBackedDay)) return;
            if (!Rand.Chance(TalkBackMath.JoinChance(social.Warmth(listener, teller)))) return;
            w.talkBackedDay = today;
            JoinInTalk(w, listener, teller);
        }

        /// <summary>The listener joins the wish they heard about: a line each over both heads, a word in the news, as a co-signer.</summary>
        private void JoinInTalk(Wish w, Pawn p, Pawn teller)
        {
            // together: the two are talking face to face (the game's own conversation that brought the wish up).
            w.coSigners.Add(p);
            social.Bump(p, teller, TieKind.Friends, 4f, "CC_TieNote_Petition".Translate(w.Label));
            w.expiresTick += GenDate.TicksPerDay * 2;
            SetCooldown(p, w.def);
            var tags = Flavor.TagsFor(p, w).ToArray();
            string join = Flavor.FromPool("Rimfolk_Pool_TalkBacked_Join", p, w, ToneUtility.ToneFor(p), tags);
            if (!join.NullOrEmpty()) Speech.Say(p, join.Formatted(p.Named("PAWN"), teller.Named("OTHER"), w.Label.Named("WISH")).Resolve(), teller, SpeechBacking);
            string glad = Flavor.FromPool("Rimfolk_Pool_TalkBacked_Glad", teller, w, ToneUtility.ToneFor(teller));
            if (!glad.NullOrEmpty()) Speech.Say(teller, glad.Formatted(teller.Named("PAWN"), p.Named("OTHER"), w.Label.Named("WISH")).Resolve(), p, SpeechBacking);
            Tell.Message(NewsKind.Colony, Importance.Notable, null,
                "CC_Life_TalkBacked".Translate(p.Named("PAWN"), teller.Named("OTHER"), w.LabelInline.Named("WISH"), (w.coSigners.Count + 1).Named("COUNT")).Resolve(),
                new LookTargets(p), MessageTypeDefOf.NeutralEvent);
            AddChronicle(new ChronicleEntry(ChronicleKind.Petition, "CC_Chronicle_TalkBacked".Translate(p.LabelShort, teller.LabelShort, w.LabelInline).Resolve(), p));
        }
    }
}
