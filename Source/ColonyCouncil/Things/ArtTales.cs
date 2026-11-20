using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Grammar;

namespace ColonyCouncil
{
    /// <summary>
    /// One of the colony's own stories as a tale of the game's (Things/ArtTales.cs): who was there (PAWN), another in it, a
    /// person or a beast (OTHER), and two words of its own: the god as the colony knew it then (GOD: its name where the colony
    /// knew it was that god, else what was seen), and what it was about (WHAT: a gift, a line's name). The game's art reads it
    /// as any tale (a sculpture's description, a title), with the place and weather of the moment.
    /// </summary>
    public class Tale_Rimfolk : Tale
    {
        public TaleData_Pawn pawnData, otherData;
        public string god, what;

        public Tale_Rimfolk()
        {
        }

        public Tale_Rimfolk(Pawn pawn, Pawn other, string god, string what)
        {
            pawnData = TaleData_Pawn.GenerateFrom(pawn);
            if (other != null) otherData = TaleData_Pawn.GenerateFrom(other);
            this.god = god ?? "";
            this.what = what ?? "";
            if (pawn.SpawnedOrAnyParentSpawned) surroundings = TaleData_Surroundings.GenerateFrom(pawn.PositionHeld, pawn.MapHeld);
        }

        public override Pawn DominantPawn => pawnData?.pawn;

        public override string ShortSummary => def.LabelCap + ": " + (pawnData?.name?.ToStringShort ?? "?");

        public override bool Concerns(Thing th) => base.Concerns(th) || th != null && (pawnData?.pawn == th || otherData?.pawn == th);

        public override void Notify_FactionRemoved(Faction faction)
        {
            if (pawnData?.faction == faction) pawnData.faction = null;
            if (otherData?.faction == faction) otherData.faction = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref pawnData, "pawnData");
            Scribe_Deep.Look(ref otherData, "otherData");
            Scribe_Values.Look(ref god, "god");
            Scribe_Values.Look(ref what, "what");
        }

        protected override IEnumerable<Rule> SpecialTextGenerationRules(Dictionary<string, string> outConstants)
        {
            if (pawnData != null)
                foreach (var r in pawnData.GetRules("PAWN", outConstants))
                    yield return r;
            if (otherData != null)
                foreach (var r in otherData.GetRules("OTHER", outConstants))
                    yield return r;
            yield return new Rule_String("GOD", god.NullOrEmpty() ? "CC_Tale_SomethingUnnamed".Translate().ToString() : god);
            yield return new Rule_String("WHAT", what ?? "");
        }

        public override void GenerateTestData()
        {
            base.GenerateTestData();
            pawnData = TaleData_Pawn.GenerateRandom(true);
            otherData = TaleData_Pawn.GenerateRandom(true);
            god = "a great white thrumbo";
            what = "a carved figure";
        }
    }

    /// <summary>
    /// The gods, the beasts and the land in the colony's art (docs/content-update-proposals.md, C17; the player's go of 20
    /// November 2026): Rimfolk's moments recorded as the game's own tales (Defs/TaleDefs/Rimfolk_Tales.xml), so a sculpture
    /// made later may show one, in the game's own words for art ("This sculpture depicts Avila kneeling before a great white
    /// beast at dusk"): a god seen, a sacred beast's visit, an offering taken before everyone's eyes, a festival kept (and a
    /// god's beast walked to its place on the day), the beast that stays given its name, the herd's old leader dying, a raid
    /// held at a line, a named season a believer took for a god's doing (Gods/Among/SeasonGods.cs, C18). A god is named only by the colony's name where the colony knew at the time it was that god, else as
    /// what was seen (GodDef.unheard). Vanilla Furniture Expanded - Art's statues take the same tales (they use the game's art
    /// system). A kind of tale comes at most once in a few days at a colony (ArtTalesMath.GapDays); nothing else is saved.
    /// </summary>
    public static class ArtTales
    {
        public const string GodSeen = "Rimfolk_Tale_GodSeen", SacredBeast = "Rimfolk_Tale_SacredBeast", OfferingTaken = "Rimfolk_Tale_OfferingTaken",
            Festival = "Rimfolk_Tale_FestivalKept", FestivalBeast = "Rimfolk_Tale_FestivalBeast", BeastNamed = "Rimfolk_Tale_BeastNamed",
            HerdLeader = "Rimfolk_Tale_HerdLeaderDied", LineHeld = "Rimfolk_Tale_LineHeld", SeasonTaken = "Rimfolk_Tale_SeasonTaken";

        /// <summary>When each kind was last recorded at each home (this session: a load starts again, which only lets one more come).</summary>
        private static readonly Dictionary<string, int> last = new Dictionary<string, int>();

        private static Game lastGame;

        /// <summary>Dev: the next tale is recorded whatever its kind's gap.</summary>
        internal static bool force;

        /// <summary>
        /// The moment recorded as a tale of the game's: <paramref name="pawn"/> one of the colony's people who was there,
        /// <paramref name="other"/> a person or beast in it, <paramref name="god"/> the god as the colony knew it, <paramref name="what"/>
        /// what it was about. Null when it isn't (a kind just recorded at this home, no one of the colony, the def not loaded).
        /// </summary>
        public static Tale Record(string kind, Pawn pawn, Pawn other, string god, string what)
        {
            if (pawn == null || pawn.Dead || !pawn.RaceProps.Humanlike || pawn.Faction != Faction.OfPlayer || Find.TaleManager == null) return null;
            var def = DefDatabase<TaleDef>.GetNamedSilentFail(kind);
            if (def == null) return null;
            try
            {
                if (lastGame != Current.Game)
                {
                    lastGame = Current.Game;
                    last.Clear();
                }
                int now = Find.TickManager.TicksGame;
                string key = kind + "|" + Homes.KeyOf(pawn.MapHeld);
                if (!force && !ArtTalesMath.Due(kind, last.TryGetValue(key, out int t) ? t : -1, now)) return null;
                last[key] = now;
                var tale = new Tale_Rimfolk(pawn, other, god, what)
                {
                    def = def,
                    id = Find.UniqueIDsManager.GetNextTaleID(),
                    date = Find.TickManager.TicksAbs
                };
                Find.TaleManager.Add(tale);
                return tale;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("art tales: " + kind, e);
                return null;
            }
        }

        /// <summary>The god as the colony knew it at the moment: its name where the colony knows it was that god, else what it was seen as.</summary>
        public static string GodWords(God g, Map map) => g == null ? null : Gods.CardName(g, map) ?? g.def?.unheard ?? GodLore.LabelFor(g.Key);

        /// <summary>A god's moment seen (Gods.Moment), by its first witness.</summary>
        public static void Seen(God g, Map map, List<Pawn> witnesses, PresenceKind kind)
        {
            if (g == null || witnesses == null || kind == PresenceKind.Omen) return;
            var p = witnesses.FirstOrDefault(x => x != null && x.IsColonist);
            if (p != null) Record(GodSeen, p, null, GodWords(g, map), null);
        }
    }
}
