using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Each god's sacred animals (docs/content-update-proposals.md, C2): the god's own kinds (GodDef.sacredAnimals, from the
    /// game and the player's mods, looked up softly, so a mod that isn't loaded leaves its kinds out), else its domain's
    /// (GodDomainDef.sacredAnimals) as before. Rare kinds only, never the land's common game, so hunting does not offend all
    /// the time. What reads them: a sacred beast killed is an offence to its god (GodDeeds, GodHealth), the vow to spare a
    /// beast (Vows), the god's dream warnings (GodDreams), the beast a god's people bring (GodWorldGifts), the god's mark
    /// never one of them (GodMarkGoods), a god's beast at home and the beast that stays taken for a god's (GodBeasts).
    /// Hidden, as everything the gods are: what the colony knows of it is only what a believer says.
    /// </summary>
    public static class SacredKinds
    {
        private static Dictionary<ThingDef, List<God>> byRace;
        private static int cacheVersion = -1;
        private static Game cacheGame;

        /// <summary>The god's own sacred kinds that are loaded, or none.</summary>
        public static List<ThingDef> OwnOf(God g) => g?.def?.Animals ?? new List<ThingDef>();

        /// <summary>The god has sacred kinds of its own (all but Pip, the god of machines).</summary>
        public static bool Own(God g) => OwnOf(g).Count > 0;

        /// <summary>The god's sacred kinds: its own, else its domain's.</summary>
        public static List<ThingDef> Of(God g)
        {
            if (g == null) return new List<ThingDef>();
            var own = OwnOf(g);
            return own.Count > 0 ? own : g.DomainDef?.Animals ?? new List<ThingDef>();
        }

        /// <summary>The gods (not gone) whose own sacred kinds include this race.</summary>
        public static List<God> GodsOf(ThingDef race)
        {
            if (race == null || !Gods.On) return new List<God>();
            if (byRace == null || cacheVersion != Gods.Version || cacheGame != Current.Game)
            {
                byRace = new Dictionary<ThingDef, List<God>>();
                cacheVersion = Gods.Version;
                cacheGame = Current.Game;
                foreach (var g in Gods.All)
                {
                    if (g == null || g.kind == GodKind.Dead) continue;
                    foreach (var r in OwnOf(g))
                    {
                        if (!byRace.TryGetValue(r, out var list)) byRace[r] = list = new List<God>();
                        if (!list.Contains(g)) list.Add(g);
                    }
                }
            }
            return byRace.TryGetValue(race, out var found) ? found : new List<God>();
        }

        /// <summary>Some god holds this race as its own sacred kind.</summary>
        public static bool AnySacred(ThingDef race) => GodsOf(race).Count > 0;

        /// <summary>
        /// The god whose beast this kind is: a god that holds it as its own sacred kind, else a god whose shape walks as it
        /// (GodShapes.Beasts: Bellwether's thrumbo, Tamiu's great cats); null for none.
        /// </summary>
        public static God BeastOf(ThingDef race)
        {
            if (race == null || !Gods.On) return null;
            var own = GodsOf(race).FirstOrDefault();
            if (own != null) return own;
            return Gods.All.FirstOrDefault(g => g != null && g.kind != GodKind.Dead && GodShapes.Beasts(g)?.Contains(race) == true);
        }

        /// <summary>A sacred kind's death at the colony's hand, for those gods that hold it as their own: each displeased as its domain's deed was (GodsMath.SacredDeath).</summary>
        public static void Killed(Pawn victim, Map map, bool slaughtered, bool wild, string who)
        {
            foreach (var g in GodsOf(victim?.def))
            {
                float by = GodsMath.SacredDeath(g.domain, slaughtered, wild);
                if (by == 0f) continue;
                string what = g.DomainDef?.ofWords ?? "";
                string why = (slaughtered ? "CC_God_Deed_SacredSlaughtered" : "CC_God_Deed_SacredHunted")
                    .Translate(victim.def.label.Named("KIND"), what.Named("WHAT"), who.Named("WHO")).Resolve();
                Gods.AddFavour(g, map, by, why, FavourSource.Sacrilege);
            }
        }
    }
}
