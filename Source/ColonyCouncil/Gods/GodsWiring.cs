using System;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' two halves joined (docs/design/gods.md, "the contract"): the core's seams (GodPresenceSeam, GodLookSeam)
    /// call the presence part (GodPresence, GodAvatar) with the god's own look and colours, and named only as the colony
    /// knows it: its name in the colony's book once someone has heard of it, else what people see of it ("a great white
    /// thrumbo"; GodLore.LabelFor). Its epithet isn't put on its card: the card says what was seen, not who the god is.
    /// The look is the god's own (God.look, from its def), else its domain's in its people's age.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GodsWiring
    {
        static GodsWiring()
        {
            GodPresenceSeam.Manifest = s =>
            {
                // The card names the god only when the colony knows it was its doing (Gods.CardName), or a god reveals itself
                // in front of everyone; otherwise it is veiled: what was seen, no name.
                string known = Gods.CardName(s.god, s.map);
                bool veiled = s.veiled && known == null && s.kind != PresenceKind.Reveal;
                // The colony's sign of it on the card, only when the card names it and the colony has been told what it is.
                string sign = !veiled && GodSigns.Known(s.god) ? GodSigns.KeyOf(s.god) : null;
                return GodPresence.Manifest(LookOf(s.god), known ?? GodLore.LabelFor(Gods.Name(s.god)), null, s.map, s.cell, s.kind, s.avatar, s.title, s.text, veiled,
                    s.god.colour, s.god.colour2, sign);
            };
            GodLookSeam.MakeAvatar = (p, god, disguised) => GodAvatar.Make(p, LookOf(god), Gods.Name(god), disguised, god.colour, god.colour2);
            HediffComp_GodAvatar.Label = GodLore.LabelFor;
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
