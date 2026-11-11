using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' two halves joined (docs/design/gods.md, "the contract"): the core's seams (GodPresenceSeam,
    /// GodLookSeam) call the presence part (GodPresence, GodAvatar) with the god's own look, name, epithet and colours.
    /// The look is the god's own (God.look, "Rimfolk_GodLook_&lt;Domain&gt;"), else its domain's in its people's age.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GodsWiring
    {
        static GodsWiring()
        {
            GodPresenceSeam.Manifest = s => GodPresence.Manifest(LookOf(s.god), Gods.Name(s.god), Gods.Epithet(s.god), s.map, s.cell, s.kind, s.avatar, s.title,
                s.text, s.veiled, s.god.colour, s.god.colour2);
            GodLookSeam.MakeAvatar = (p, god, disguised) => GodAvatar.Make(p, LookOf(god), Gods.Name(god), disguised, god.colour, god.colour2);
        }

        /// <summary>The god's look: its own, else its domain's in the age of its people (0 tribal to 3 spacer).</summary>
        public static GodLookDef LookOf(God god)
        {
            if (god == null) return null;
            return GodLooks.Named(god.look) ?? GodLooks.For(god.domain.ToString(), AgeOf(god.age));
        }

        public static TechLevel AgeOf(int age) => age <= 0 ? TechLevel.Neolithic : age == 1 ? TechLevel.Medieval : age == 2 ? TechLevel.Industrial : TechLevel.Spacer;
    }
}
