using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Strangers as other mods make them: a Hospitality guest, a visitor of the Empire or a Vanilla Expanded people, a
    /// travelling mage of A RimWorld of Magic, an Alpha Animals creature. Encounters and dilemmas ask for them through
    /// <see cref="StrangerRole"/> (guest, factions, races, tags), and their lines through tags on X (x:guest,
    /// x:faction:Empire, x:race:AA_MeadowAve, x:mage). Everything is matched by name, so with the mod missing nobody
    /// fits and the content never comes up.
    /// </summary>
    public static class ModStrangers
    {
        /// <summary>The mod-related conditions of a stranger role (the rest are in <see cref="Strangers.Fits"/>).</summary>
        public static bool Fits(StrangerRole r, Pawn x)
        {
            if (r == null || x == null) return true;
            if (!r.guest && !r.notGuest && r.factions.NullOrEmpty() && r.races.NullOrEmpty() && r.tags.NullOrEmpty()) return true;
            try
            {
                if (r.guest || r.notGuest)
                {
                    bool g = Strangers.IsHospitalityGuest(x);
                    if (r.guest != g) return false;
                }
                if (!r.factions.NullOrEmpty())
                {
                    var f = Strangers.FactionOf(x) ?? x.Faction;
                    if (f?.def == null || !PeoplesByMod.Matches(f.def, r.factions)) return false;
                }
                if (!r.races.NullOrEmpty() && !r.races.Contains(x.def.defName) && (x.kindDef == null || !r.races.Contains(x.kindDef.defName))) return false;
                if (!r.tags.NullOrEmpty() && !PeopleTags.Matches(x, r.tags)) return false;
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mod strangers", e);
                return false;
            }
        }

        /// <summary>
        /// Tags for lines about X: x:guest (with x:roomgood, x:roompoor or x:nobed for the guest bed they were given,
        /// x:happyguest or x:unhappyguest for how the visit is going), x:faction:DefName, x:factionmod:packageid (the mod
        /// the people comes from, for nations named by their mod: <see cref="PeoplesByMod"/>), x:royal, x:race:DefName and
        /// X's one-word tag groups as x:mage, x:fighter, x:healer.
        /// </summary>
        public static void AddTags(HashSet<string> tags, Pawn x)
        {
            if (tags == null || x?.RaceProps == null) return;
            try
            {
                tags.Add("x:race:" + x.def.defName);
                // An animal: x:pokemon, x:shiny, x:livestock, x:young... (Animals/AnimalFacts.cs).
                AnimalTags.AddX(tags, x);
                if (!x.RaceProps.Humanlike) return;
                var f = Strangers.FactionOf(x) ?? x.Faction;
                if (f?.def != null && !f.IsPlayer)
                {
                    tags.Add("x:faction:" + f.def.defName);
                    string pkg = PeoplesByMod.PackageOf(f.def);
                    if (pkg.Length > 0) tags.Add("x:factionmod:" + pkg);
                }
                if (ModsConfig.RoyaltyActive && x.royalty?.MostSeniorTitle != null) tags.Add("x:royal");
                foreach (var t in PeopleTags.Identity(x))
                    if (t.IndexOf(':') < 0) tags.Add("x:" + t);
                if (Strangers.IsHospitalityGuest(x))
                {
                    tags.Add("x:guest");
                    var room = Guests.RoomOf(x);
                    if (room == null) tags.Add("x:nobed");
                    else
                    {
                        float imp = RoomUtil.Impressiveness(room);
                        if (imp >= 50f) tags.Add("x:roomgood");
                        else if (imp < 25f) tags.Add("x:roompoor");
                    }
                    float score = Guests.VisitScore(x);
                    if (score >= 0.7f) tags.Add("x:happyguest");
                    else if (score >= 0f && score < 0.35f) tags.Add("x:unhappyguest");
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mod stranger tags", e);
            }
        }
    }

    /// <summary>
    /// The colony-side conditions of encounters written for other mods (<see cref="EncounterDef.mods"/>,
    /// <see cref="EncounterDef.colonyHas"/>, <see cref="EncounterDef.biomes"/>): the mod is on, the colony has the
    /// mod's cheese press or wine, the map is one of the mod's biomes. What the colony has is looked up at most once
    /// an in-game hour per map.
    /// </summary>
    public static class ModContent
    {
        private static readonly Dictionary<(int map, string def), (int tick, bool has)> hasCache = new Dictionary<(int, string), (int, bool)>();
        private static Game hasCacheGame;

        public static bool Fits(EncounterDef d, Map map)
        {
            if (d == null) return false;
            if (!d.mods.NullOrEmpty() && !d.mods.Any(m => ModsConfig.IsActive(m) || People.ModActive(m))) return false;
            if (!d.biomes.NullOrEmpty() && (map?.Biome == null || !d.biomes.Contains(map.Biome.defName))) return false;
            if (!d.colonyHas.NullOrEmpty() && !d.colonyHas.Any(n => Has(map, n))) return false;
            return true;
        }

        /// <summary>The colony has one of these on the map: built (its own building) or in its stores.</summary>
        public static bool Has(Map map, string defName)
        {
            if (map == null || defName.NullOrEmpty()) return false;
            // Map ids start over in every game: what another save's map had says nothing about this one.
            if (hasCacheGame != Current.Game)
            {
                hasCache.Clear();
                hasCacheGame = Current.Game;
            }
            int now = Find.TickManager?.TicksGame ?? 0;
            var key = (map.uniqueID, defName);
            if (hasCache.TryGetValue(key, out var c) && now >= c.tick && now - c.tick < GenDate.TicksPerHour) return c.has;
            if (hasCache.Count > 400) hasCache.Clear();
            bool has = false;
            try
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def != null)
                {
                    if (def.category == ThingCategory.Building)
                        has = map.listerBuildings.AllBuildingsColonistOfDef(def).Any();
                    else
                        has = map.listerThings.ThingsOfDef(def).Any(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage());
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("mod content " + defName, e);
            }
            hasCache[key] = (now, has);
            return has;
        }
    }

    /// <summary>
    /// Hospitality, read by name (Hospitality.Utilities.GuestUtility in its current version, Hospitality.GuestUtility in
    /// older ones): which guest bed a guest sleeps in, how their visit is going, and its own way of taking a guest in.
    /// </summary>
    public static class Guests
    {
        public const string Mod = "orion.hospitality";

        private static bool looked;
        private static MethodInfo visitScore, adopt;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                var type = GenTypes.GetTypeInAnyAssembly("Hospitality.Utilities.GuestUtility") ?? GenTypes.GetTypeInAnyAssembly("Hospitality.GuestUtility");
                if (type == null) return;
                foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var ps = m.GetParameters();
                    if (m.Name == "GetVisitScore" && ps.Length == 2 && ps[0].ParameterType == typeof(Pawn) && m.ReturnType == typeof(bool)) visitScore = m;
                    else if (m.Name == "Adopt" && ps.Length == 1 && ps[0].ParameterType == typeof(Pawn)) adopt = m;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hospitality lookup", e);
            }
        }

        /// <summary>The colony's guest beds on a map.</summary>
        public static IEnumerable<Building_Bed> Beds(Map map) =>
            map == null ? Enumerable.Empty<Building_Bed>() : map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().Where(b => b.Spawned && GuestBeds.IsGuestBed(b));

        /// <summary>The room of the guest bed this guest sleeps in, or null.</summary>
        public static Room RoomOf(Pawn guest)
        {
            if (guest?.Map == null) return null;
            var owned = guest.ownership?.OwnedBed;
            if (owned != null && owned.Spawned) return owned.GetRoom();
            foreach (var b in Beds(guest.Map))
                if (b.OwnersForReading != null && b.OwnersForReading.Contains(guest)) return b.GetRoom();
            return null;
        }

        /// <summary>How the visit is going for them, 0..1 (Hospitality's own score), or -1 when it can't be read.</summary>
        public static float VisitScore(Pawn guest)
        {
            Look();
            if (visitScore == null || guest == null) return -1f;
            try
            {
                var args = new object[] { guest, 0f };
                return (bool)visitScore.Invoke(null, args) ? (float)args[1] : -1f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hospitality visit score", e);
                return -1f;
            }
        }

        /// <summary>Hospitality's own "they stay with us" (as its join request letter does it). False when it couldn't.</summary>
        public static bool Adopt(Pawn guest)
        {
            Look();
            if (adopt == null || guest == null || guest.Dead || !guest.Spawned) return false;
            try
            {
                // Like Hospitality's own letter: the last of a group taken in doesn't count as a group lost.
                var lord = guest.GetLord();
                if (lord != null && lord.ownedPawns.Count == 1)
                {
                    var field = lord.LordJob?.GetType().GetField("getUpsetWhenLost", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(bool)) field.SetValue(lord.LordJob, false);
                }
                adopt.Invoke(null, new object[] { guest });
                return guest.Faction == Faction.OfPlayer;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hospitality adopt", e);
                return false;
            }
        }

        public static bool CanAdopt
        {
            get
            {
                Look();
                return adopt != null;
            }
        }

        /// <summary>
        /// A fix to Hospitality: it keeps each guest's record (its CompGuest: arrived or not, the areas set on the guest
        /// tab) in a table of its own, by pawn, and never empties it. The game counts two pawns as the same when they
        /// have the same ID, so after another save is loaded in the same session, a guest is read from their earlier
        /// self's record, from the save loaded before: one that hadn't arrived yet keeps the Recruit button grey ("This
        /// guest can not be recruited (right now)") and what's set on the guest tab goes to the old record. The table is
        /// emptied as each game starts and once it has loaded; Hospitality fills it again from the guests themselves.
        /// </summary>
        public static void ForgetLastGame()
        {
            try
            {
                var type = GenTypes.GetTypeInAnyAssembly("Hospitality.Utilities.CompUtility") ?? GenTypes.GetTypeInAnyAssembly("Hospitality.CompUtility");
                var field = type?.GetField("guestComps", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                (field?.GetValue(null) as System.Collections.IDictionary)?.Clear();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hospitality guest records", e);
            }
        }
    }

    /// <summary>
    /// A dilemma about a Hospitality guest (X; the def's stranger role should ask for guest). Only for a guest who isn't
    /// a royal (Hospitality never lets those join), whose visit is going at least this well (<see cref="minVisitScore"/>,
    /// Hospitality's own score, 0..1; ignored when it can't be read), and who would stay of their own will (GuestWill).
    /// </summary>
    public class DilemmaWorker_Guest : DilemmaWorker_Stranger
    {
        protected virtual float MinVisitScore => 0.5f;

        protected override bool Extra(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (x == null || !Strangers.IsHospitalityGuest(x)) return false;
            if (x.royalty != null && x.royalty.AllTitlesInEffectForReading.Count > 0) return false;
            float score = Guests.VisitScore(x);
            if (score >= 0f && score < MinVisitScore) return false;
            // Only someone who'd stay of their own will asks to (Integrations/GuestWill.cs).
            if (!GuestWill.WouldStay(x)) return false;
            return base.Extra(ctx);
        }
    }

    /// <summary>The same, for a guest whose visit is going badly (a complaint, a grievance).</summary>
    public class DilemmaWorker_UnhappyGuest : DilemmaWorker_Stranger
    {
        protected override bool Extra(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (x == null || !Strangers.IsHospitalityGuest(x)) return false;
            float score = Guests.VisitScore(x);
            if (score < 0f || score > 0.4f) return false;
            return base.Extra(ctx);
        }
    }

    /// <summary>
    /// A Hospitality guest stays on as one of the colony, taken in the way Hospitality itself does it (its join
    /// request). Unavailable for anyone who isn't a guest, or when Hospitality's own way can't be found.
    /// </summary>
    public class DilemmaEffect_AdoptGuest : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (x == null || x.Dead || !x.Spawned) return "CC_Stranger_Gone".Translate(x?.LabelShort ?? "?").ToString();
            if (!Strangers.IsHospitalityGuest(x) || !Guests.CanAdopt) return "CC_PM_Guest_CantStay".Translate(x.LabelShort).ToString();
            return null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx.stranger;
            if (x == null || x.Dead || !x.Spawned || x.Faction == Faction.OfPlayer || !Strangers.IsHospitalityGuest(x)) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            Guests.Adopt(x);
        }
    }
}
