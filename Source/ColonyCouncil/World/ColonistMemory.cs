using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A people's memory of single colonists (docs/design/stage1-region.md, item 7; decision 13). Each people remembers
    /// some colonists by name for what they did: who broke a raiders' camp that troubled their roads (those who fought on
    /// its map), who fought beside their fighters, who traded at their post, who walked their settlers out; and the wrongs:
    /// who killed one of theirs in a fight (the death's instigator), who executed or cut open one of their prisoners, who
    /// threw hard words at one of theirs. Deeds the peoples already remember by a colonist's name (a prisoner set free, a
    /// wounded guest tended, a brawl: World/KinMemory.cs) come in too.
    /// <para>
    /// Kept on the world ledger: one Regard tie from the people to the colonist (origin "colonist"), the marks in its key
    /// (ColonistMemoryMath packs them), its number the standing when last written. Six colonists a people, 48 in all, the
    /// one remembered least going first; marks fade as the peoples' deeds do, and a tie whose marks have all faded ends.
    /// </para>
    /// <para>
    /// What it does: their notables' regard of each colonist (World/Notables.cs), the colonist talk and letters name
    /// ({FRIEND}, {FOE}, the facts service's their:foe, you:foe, you:remembered), the visitors who ask for their friend
    /// (KinMemory.Greeter), and their traders, who won't deal with a colonist their people hold a death against: the trade
    /// option says so, and a caravan at their town sends someone else to trade. No raid of its own: a vendetta stays the
    /// colony's own story.
    /// </para>
    /// </summary>
    public static class ColonistMemory
    {
        /// <summary>The origin of the ledger's regard ties kept here.</summary>
        public const string Origin = "colonist";

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The colony's memory is on (the same switch as the peoples' memory of the colony).</summary>
        public static bool On => FactionMemory.On && WorldLedger.Instance != null;

        // ---------------- The store ----------------

        private static Dictionary<(int people, int person), Tie> index;
        private static int indexVersion = -1;
        private static Game indexGame;

        /// <summary>The live ties kept here, by people and person, found again whenever the ledger changes.</summary>
        private static Dictionary<(int people, int person), Tie> Index
        {
            get
            {
                if (index == null || indexVersion != WorldLedger.Version || indexGame != Current.Game)
                {
                    index ??= new Dictionary<(int, int), Tie>();
                    index.Clear();
                    foreach (var t in WorldLedger.OwnTies(Tie.Kind.Regard))
                        if (t != null && t.Live && t.origin == Origin && t.from.kind == RefKind.People && t.to.kind == RefKind.Person)
                            index[(t.from.id, t.to.id)] = t;
                    indexVersion = WorldLedger.Version;
                    indexGame = Current.Game;
                }
                return index;
            }
        }

        private static Tie TieOf(Faction f, Pawn p)
        {
            if (f == null || p == null) return null;
            return Index.TryGetValue((f.loadID, p.thingIDNumber), out var t) ? t : null;
        }

        private static List<ColonistMemoryMath.Mark> Marks(Tie t) => ColonistMemoryMath.Unpack(t?.terms?.key);

        // ---------------- Remembering ----------------

        /// <summary>One of the memory's own marks (ColonistMemoryMath.Kinds) for this colonist, by this people; <paramref name="factor"/> scales its weight.</summary>
        public static void Note(Faction f, Pawn colonist, string kind, float factor = 1f)
        {
            if (kind == null || !ColonistMemoryMath.Kinds.TryGetValue(kind, out var k)) return;
            Add(f, colonist, kind, k.sign, Mathf.Max(1, Mathf.RoundToInt(k.weight * factor)), k.mergeDays, k.max);
        }

        /// <summary>A deed of the peoples' memory that names a colonist (KinMemory.Name): remembered of them too, with its own kind and size.</summary>
        public static void FromDeed(FactionDeed d, Pawn colonist)
        {
            if (d == null || d.faction == null) return;
            int sign = d.side == DeedSide.Favor ? 1 : d.side == DeedSide.Wrong ? -1 : 0;
            if (sign == 0) return;
            Add(d.faction, colonist, d.kind, sign, d.weight, 3f, MemoryMath.MaxWeight);
        }

        private static void Add(Faction f, Pawn colonist, string kind, int sign, int weight, float mergeDays, int max)
        {
            if (!On || !FactionMemory.Remembers(f) || f.defeated || !KinMemory.Ours(colonist) || kind.NullOrEmpty()) return;
            try
            {
                int now = Now;
                var t = TieOf(f, colonist);
                var marks = Marks(t);
                ColonistMemoryMath.Add(marks, kind, sign, weight, now, mergeDays, max);
                float standing = ColonistMemoryMath.Standing(marks, now);
                if (t != null)
                {
                    t.terms.key = ColonistMemoryMath.Pack(marks);
                    t.terms.number = standing;
                    WorldLedger.Touch(t);
                    return;
                }
                MakeRoom(f, now);
                var from = Refs.Of(f);
                var to = WorldLedger.PersonOf(colonist);
                WorldLedger.AddTie(Tie.Kind.Regard, from, to, new TieTerms { key = ColonistMemoryMath.Pack(marks), number = standing }, Tie.Never, Origin);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colonist memory: " + kind, e);
            }
        }

        /// <summary>Room for one more colonist this people remembers: past the bounds, the one remembered least is let go.</summary>
        private static void MakeRoom(Faction f, int now)
        {
            var mine = Index.Values.Where(t => t.from.id == f.loadID).ToList();
            End(mine, ColonistMemoryMath.MaxPerPeople - 1, now);
            End(Index.Values.ToList(), ColonistMemoryMath.MaxAll - 1, now);
        }

        private static void End(List<Tie> ties, int keep, int now)
        {
            while (ties.Count > keep && keep >= 0)
            {
                int i = ColonistMemoryMath.Weakest(ties.Select(t => ColonistMemoryMath.Standing(Marks(t), now)).ToList());
                if (i < 0) return;
                WorldLedger.Set(ties[i], Tie.State.Ended);
                ties.RemoveAt(i);
            }
        }

        /// <summary>The day's look: a colonist whose marks have all faded, or whose people are gone, is no longer remembered by name.</summary>
        internal static void Daily(int now)
        {
            if (WorldLedger.Instance == null) return;
            foreach (var t in Index.Values.ToList())
            {
                var f = Refs.Faction(t.from);
                if (f == null || f.defeated || ColonistMemoryMath.Faded(Marks(t), now)) WorldLedger.Set(t, Tie.State.Ended);
            }
        }

        // ---------------- Hooks for what comes later ----------------

        /// <summary>
        /// A colonist traded at a people's trading post (the trading posts' release calls this when a trade there is done):
        /// remembered a little, a run of trades adding up.
        /// </summary>
        public static void TradedAtPost(Faction f, Pawn negotiator, float value)
        {
            if (value < 200f) return;
            Note(f, negotiator, "tradedPost", value >= 3000f ? 3f : value >= 1000f ? 2f : 1f);
        }

        /// <summary>Colonists walked a people's settlers out to found a hamlet (the hamlets' release calls this when it stands).</summary>
        public static void WalkedSettlers(Faction f, IEnumerable<Pawn> escorts)
        {
            if (escorts == null) return;
            foreach (var p in escorts.Distinct().Take(ColonistMemoryMath.MaxFought)) Note(f, p, "walkedSettlers");
        }

        /// <summary>
        /// A raiders' camp broken (World/Region/RaidersCamp.cs): the peoples at peace with the colony with a town near it
        /// remember the colony for it, and those of the colony who fought on its map by name.
        /// </summary>
        public static void CampBroken(Map home, RaidersCamp c, PlanetTile tile)
        {
            if (!FactionMemory.On || c?.people == null || !tile.Valid) return;
            try
            {
                foreach (var f in Troubled(c.people, tile))
                {
                    FactionMemory.Note(f, "brokeCamp", c.kind);
                    if (c.fought == null) continue;
                    foreach (var p in c.fought.Where(KinMemory.Ours).Take(ColonistMemoryMath.MaxFought)) Note(f, p, "campBroken");
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colonist memory: a camp broken", e);
            }
        }

        /// <summary>The peoples a camp troubled: at peace with the colony, a town within ColonistMemoryMath.TroubledTiles of it, the nearest first.</summary>
        private static List<Faction> Troubled(Faction camp, PlanetTile tile)
        {
            var near = new Dictionary<Faction, float>();
            foreach (var s in Find.WorldObjects.Settlements)
            {
                var f = s?.Faction;
                if (f == null || f == camp || f.IsPlayer || s.Destroyed || !FactionMemory.Remembers(f) || f.HostileTo(Faction.OfPlayer) || s.Tile.Layer != tile.Layer) continue;
                float d = Find.WorldGrid.ApproxDistanceInTiles(s.Tile, tile);
                if (d > ColonistMemoryMath.TroubledTiles) continue;
                if (!near.TryGetValue(f, out float was) || d < was) near[f] = d;
            }
            return near.OrderBy(kv => kv.Value).Take(ColonistMemoryMath.MaxTroubled).Select(kv => kv.Key).ToList();
        }

        /// <summary>
        /// Someone of a people died. A colonist dealt the blow: an execution if they were the colony's prisoner, else one
        /// of theirs killed in a fight (in their raid on the colony, elsewhere, or at peace). An enemy of the colony killed
        /// by a colonist with fighters of a people at peace beside them: those peoples remember who fought beside them.
        /// </summary>
        internal static void Died(Faction f, Pawn member, DamageInfo? dinfo, Map map, bool hostile, bool prisoner)
        {
            if (f == null || member == null || f.IsPlayer || member.RaceProps == null || !member.RaceProps.Humanlike) return;
            var killer = dinfo?.Instigator as Pawn;
            if (!KinMemory.Ours(killer)) return;
            if (prisoner)
            {
                Note(f, killer, "executed");
                return;
            }
            // Killed while the colony was turning on them (their traders robbed, their camp attacked): a killing, as the
            // peoples' memory of the colony counts it (World/Patch_FactionMemory.cs), not one of their raiders fallen.
            var mem = FactionMemory.Instance;
            bool turnedOn = mem != null && mem.SinceLast(f, DeedSide.Wrong) < GenDate.TicksPerDay
                            && mem.Best(f, DeedSide.Wrong, 0f, 0f, false, d => d.kind == "robbed" || d.kind == "attacked" || d.kind == "campHarmed") != null;
            if (turnedOn) hostile = false;
            Note(f, killer, ColonistMemoryMath.KillKind(hostile, hostile && map != null && map.IsPlayerHome));
            if (hostile && map != null) Beside(killer, map);
        }

        /// <summary>The peoples whose fighters stood beside this colonist in the fight just now (on the map, at peace with the colony, fighting, near).</summary>
        private static void Beside(Pawn colonist, Map map)
        {
            if (!colonist.Spawned || colonist.Map != map) return;
            var seen = new HashSet<Faction>();
            float range = ColonistMemoryMath.BesideCells * ColonistMemoryMath.BesideCells;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                var f = p?.Faction;
                if (f == null || f.IsPlayer || seen.Contains(f) || p.RaceProps == null || !p.RaceProps.Humanlike || p.Downed || p.IsPrisoner) continue;
                if (f.HostileTo(Faction.OfPlayer) || !FactionMemory.Remembers(f)) continue;
                if ((p.Position - colonist.Position).LengthHorizontalSquared > range || !Fighting(p)) continue;
                seen.Add(f);
                Note(f, colonist, "foughtBeside");
                if (seen.Count >= ColonistMemoryMath.MaxBeside) return;
            }
        }

        /// <summary>In the fight: has someone to fight, or is there to fight beside the colony (the game's aid that came when it called).</summary>
        private static bool Fighting(Pawn p) =>
            p.mindState?.enemyTarget != null || p.GetLord()?.LordJob is LordJob_AssistColony || p.CurJobDef == JobDefOf.AttackMelee || p.CurJobDef == JobDefOf.AttackStatic;

        /// <summary>One of a people's own, a prisoner of the colony, cut open for their organs by a colony doctor.</summary>
        internal static void Harvested(Pawn victim, Pawn doctor)
        {
            if (victim == null || doctor == null || !victim.RaceProps.Humanlike) return;
            var f = victim.Faction;
            if (f == null || f.IsPlayer) f = victim.guest?.HostFaction == Faction.OfPlayer ? Outsiders.PeopleOf(victim) : null;
            if (f == null || f.IsPlayer) return;
            Note(f, doctor, "harvested");
        }

        // ---------------- Who they remember ----------------

        /// <summary>How a people stands toward one colonist now (0 when they don't remember them by name).</summary>
        public static float Standing(Faction f, Pawn p)
        {
            if (!On || f == null || p == null) return 0f;
            var t = TieOf(f, p);
            return t == null ? 0f : ColonistMemoryMath.Standing(Marks(t), Now);
        }

        /// <summary>The colonists a people remembers by name and are still with the colony, with their standing and strongest mark of a side.</summary>
        private static IEnumerable<(Pawn pawn, float standing, ColonistMemoryMath.Mark mark)> Remembered(Faction f, int sign)
        {
            if (!On || f == null) yield break;
            int now = Now;
            foreach (var t in Index.Values)
            {
                if (t.from.id != f.loadID) continue;
                var p = WorldLedger.PawnOf(t.to);
                if (!KinMemory.Ours(p)) continue;
                var marks = Marks(t);
                float s = ColonistMemoryMath.Standing(marks, now);
                var m = ColonistMemoryMath.Best(marks, now, sign);
                if (m != null) yield return (p, s, m);
            }
        }

        /// <summary>The colonist this people remembers best for good turns (a standing of ColonistMemoryMath.Named or more), or null.</summary>
        public static Pawn Friend(Faction f, out float standing, out string kind)
        {
            standing = 0f;
            kind = null;
            Pawn best = null;
            foreach (var r in Remembered(f, 1))
                if (ColonistMemoryMath.Friend(r.standing) && (best == null || r.standing > standing))
                {
                    best = r.pawn;
                    standing = r.standing;
                    kind = r.mark.kind;
                }
            return best;
        }

        /// <summary>The colonist this people holds most against (a standing of -ColonistMemoryMath.Named or less), or null.</summary>
        public static Pawn Foe(Faction f, out float standing, out string kind)
        {
            standing = 0f;
            kind = null;
            Pawn worst = null;
            foreach (var r in Remembered(f, -1))
                if (ColonistMemoryMath.Foe(r.standing) && (worst == null || r.standing < standing))
                {
                    worst = r.pawn;
                    standing = r.standing;
                    kind = r.mark.kind;
                }
            return worst;
        }

        /// <summary>What one of their notables makes of the colonist from it (ColonistMemoryMath.Regard), split into the good part and the wrongs.</summary>
        public static void Regard(Faction f, Pawn p, out float good, out float bad)
        {
            good = bad = 0f;
            var t = On ? TieOf(f, p) : null;
            if (t == null) return;
            var marks = Marks(t);
            int now = Now;
            good = ColonistMemoryMath.Sum(marks, now, 1);
            bad = ColonistMemoryMath.Sum(marks, now, -1);
        }

        /// <summary>The colonists a people remembers by name, with what for ("broke the brigands' hold", "killed one of theirs"), for a notable's page.</summary>
        public static List<(Pawn pawn, string why)> Known(Faction f)
        {
            var list = new List<(Pawn, string)>();
            foreach (var r in Remembered(f, 1).Concat(Remembered(f, -1)).OrderByDescending(r => Math.Abs(r.standing)))
                if (!list.Any(x => x.Item1 == r.pawn))
                    list.Add((r.pawn, Why(r.mark)));
            return list;
        }

        /// <summary>A mark in a few words: "broke a raiders' camp", "killed one of theirs".</summary>
        public static string Why(ColonistMemoryMath.Mark m)
        {
            if (m == null) return "";
            string key = "CC_ColMem_Why_" + m.kind;
            if (key.CanTranslate()) return key.Translate(m.count.Named("COUNT")).Resolve();
            key = "CC_Memory_Deed_" + m.kind;
            return key.CanTranslate() ? key.Translate(m.count.Named("COUNT"), "".Named("NAME"), "".Named("FACTION")).Resolve() : m.kind;
        }

        /// <summary>
        /// Their traders won't deal with this colonist: a standing of ColonistMemoryMath.RefuseAt or less, with a wrong
        /// they hold against them. The reason names it, as the trade option shows it.
        /// </summary>
        public static bool Refuses(Faction f, Pawn p, out string why)
        {
            why = null;
            if (!On || f == null || p == null || f.IsPlayer || !KinMemory.Ours(p)) return false;
            var t = TieOf(f, p);
            if (t == null) return false;
            var marks = Marks(t);
            int now = Now;
            if (!ColonistMemoryMath.Refuses(ColonistMemoryMath.Standing(marks, now))) return false;
            var m = ColonistMemoryMath.Best(marks, now, -1);
            if (m == null) return false;
            why = "CC_ColMem_Refuse".Translate(p.LabelShort.Named("PAWN"), f.Name.Named("FACTION"), Why(m).Named("WHY")).Resolve();
            return true;
        }

        // ---------------- Words ----------------

        /// <summary>
        /// Tags for the lines of letters, events and camps: their:foe (their people hold something against one colonist by
        /// name: {FOE}), their:foe:killed / :executed / :harvested / :insulted..., and for the colonist A: their:foeA.
        /// </summary>
        public static void AddTags(HashSet<string> tags, Faction f, Pawn a)
        {
            if (tags == null || f == null || !On) return;
            try
            {
                var foe = Foe(f, out _, out string kind);
                if (foe != null)
                {
                    tags.Add("their:foe");
                    tags.Add("their:foe:" + ColonistMemoryMath.Tag(kind));
                    if (foe == a) tags.Add("their:foeA");
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colonist memory tags", e);
            }
        }

        /// <summary>
        /// The facts of talk (Text/Facts/FactsColony.cs): {FOE} and their:foe for the colonist their people hold most
        /// against, and for the colonist talking: you:foe (and you:foe:KIND) when that is them, you:remembered (and
        /// you:remembered:KIND) when their people remember them for a good turn of this memory's own.
        /// </summary>
        public static void AddFacts(FactSet f, Pawn c, Faction p)
        {
            if (f == null || p == null || !On) return;
            var foe = Foe(p, out _, out string kind);
            if (foe != null)
            {
                f.Tag("their:foe", 1f);
                f.Tag("their:foe:" + ColonistMemoryMath.Tag(kind), 1f);
                f.Value("FOE", foe.LabelShort);
            }
            if (c == null) return;
            var t = TieOf(p, c);
            if (t == null) return;
            var marks = Marks(t);
            int now = Now;
            float s = ColonistMemoryMath.Standing(marks, now);
            if (ColonistMemoryMath.Foe(s))
            {
                var m = ColonistMemoryMath.Best(marks, now, -1);
                f.Tag("you:foe", 2f);
                if (m != null) f.Tag("you:foe:" + ColonistMemoryMath.Tag(m.kind), 2f);
            }
            else if (ColonistMemoryMath.Friend(s))
            {
                var m = ColonistMemoryMath.Best(marks, now, 1);
                if (m == null || !ColonistMemoryMath.Kinds.ContainsKey(m.kind)) return;
                f.Tag("you:remembered", 2f);
                f.Tag("you:remembered:" + ColonistMemoryMath.Tag(m.kind), 2f);
            }
        }
    }

    // ---------------- The game's hooks ----------------

    /// <summary>Someone of a people died: if a colonist dealt the blow, their people remember who (a prisoner's death in the colony's hands, an execution).</summary>
    [HarmonyPatch(typeof(Faction), nameof(Faction.Notify_MemberDied))]
    public static class Patch_ColonistMemoryDied
    {
        public static void Prefix(Faction __instance, Pawn member, out (bool hostile, bool prisoner) __state)
        {
            __state = default;
            if (!MemoryHooks.Listening || __instance == null || member == null || __instance.IsPlayer) return;
            try
            {
                __state = (__instance.HostileTo(Faction.OfPlayer), member.IsPrisonerOfColony);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colonist memory death hook", e);
            }
        }

        public static void Postfix(Faction __instance, Pawn member, DamageInfo? dinfo, Map map, (bool hostile, bool prisoner) __state)
        {
            if (!MemoryHooks.Listening || __instance == null || __instance.IsPlayer) return;
            try
            {
                ColonistMemory.Died(__instance, member, dinfo, map, __state.hostile, __state.prisoner);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colonist memory death hook", e);
            }
        }
    }

    /// <summary>A prisoner cut open for their organs: their people remember the doctor who did it.</summary>
    [HarmonyPatch(typeof(ThoughtUtility), nameof(ThoughtUtility.GiveThoughtsForPawnOrganHarvested))]
    public static class Patch_ColonistMemoryHarvest
    {
        public static void Postfix(Pawn victim, Pawn billDoer)
        {
            if (!MemoryHooks.Listening) return;
            try
            {
                ColonistMemory.Harvested(victim, billDoer);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colonist memory harvest hook", e);
            }
        }
    }

    /// <summary>
    /// A trader of a people won't deal with a colonist their people hold a death against (decision 13): the trade option
    /// shows why, and a caravan at their town sends someone else to do the talking (the game picks its negotiator by the
    /// same check).
    /// </summary>
    [HarmonyPatch(typeof(FactionUtility), nameof(FactionUtility.CanTradeWith))]
    public static class Patch_ColonistMemoryTrade
    {
        public static void Postfix(Pawn p, Faction faction, ref AcceptanceReport __result)
        {
            if (!__result.Accepted || !RimfolkPatches.On || p == null || faction == null) return;
            try
            {
                if (ColonistMemory.Refuses(faction, p, out string why)) __result = new AcceptanceReport(why);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colonist memory trade hook", e);
            }
        }
    }
}
