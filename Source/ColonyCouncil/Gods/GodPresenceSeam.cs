using System;
using Verse;

namespace ColonyCouncil
{

    /// <summary>
    /// What a god's coming is, as the core asks the presence part for it (docs/design/gods.md, "Seams the later parts
    /// call"): the god, the map and the cell, the kind of moment, the avatar (or null), the card's title and text, and
    /// whether the card is plain or veiled ("some say"). A plain class: nothing here acts.
    /// </summary>
    public class PresenceSpec
    {
        public God god;
        public Map map;
        public IntVec3 cell = IntVec3.Invalid;
        public PresenceKind kind;
        /// <summary>The pawn or animal the god walks as, or null.</summary>
        public Pawn avatar;
        public string title, text;
        /// <summary>The card says "some say" (Felt) or "the colony says" (Believed) rather than naming it plainly (Walking).</summary>
        public bool veiled;

        /// <summary>A moment of a god at a place, veiled by how real the gods are.</summary>
        public static PresenceSpec For(God god, Map map, PresenceKind kind, IntVec3 cell = default, Pawn avatar = null) => new PresenceSpec
        {
            god = god, map = map, kind = kind, avatar = avatar, cell = cell == default ? IntVec3.Invalid : cell,
            veiled = !GodsMath.PlainCard(Gods.Realness)
        };
    }

    /// <summary>
    /// The seam to the presence part (Gods/Presence/, built beside the core): <see cref="Manifest"/> plays a god's moment
    /// (the build-up, the music, the sky, the card, the mark) and says whether it did. Null until the presence part sets it;
    /// <see cref="Call"/> does nothing then.
    /// </summary>
    public static class GodPresenceSeam
    {
        public static Func<PresenceSpec, bool> Manifest;

        /// <summary>
        /// Plays a god's moment through the presence part, if it is there and the gods are on, and writes it into the colony's
        /// book as those who saw it saw it (Gods.Moment). False when nothing was played.
        /// </summary>
        public static bool Call(PresenceSpec spec)
        {
            var m = Manifest;
            if (m == null || spec?.god == null || !Gods.On) return false;
            try
            {
                bool played = m(spec);
                if (played) Gods.Moment(spec);
                return played;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's moment", e);
                return false;
            }
        }
    }

    /// <summary>
    /// The seam to the presence part's look: <see cref="MakeAvatar"/> makes a pawn or an animal a god's avatar (larger, in its
    /// colours, its aura, its trail; or only its tells when disguised). Null until the presence part sets it;
    /// <see cref="Make"/> does nothing then.
    /// </summary>
    public static class GodLookSeam
    {
        public static Action<Pawn, God, bool> MakeAvatar;

        /// <summary>Makes <paramref name="p"/> the god's avatar, disguised or not, if the presence part is there. False when nothing was made.</summary>
        public static bool Make(Pawn p, God god, bool disguised)
        {
            var m = MakeAvatar;
            if (m == null || p == null || god == null || !Gods.On || !GodsMath.AvatarsCome(Gods.Realness)) return false;
            try
            {
                m(p, god, disguised);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: making an avatar", e);
                return false;
            }
        }
    }
}
