using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's entertainers at a town's festival (docs/design/diplomacy-screen.md, step B, point 13), and a festival a
    /// town invited the colony to by its own letter (Dealings/WorldWrites.cs): the invitation missions' own journey to their
    /// town and stay there (Missions/Workers_MissionRemembered.cs), its words its own (Defs/MissionDefs/
    /// Rimfolk_Missions_Festival.xml): a small party of the colony's goes as a caravan, plays and stays for the day, and
    /// comes home with a gift and their people's regard. Never drawn by the storyteller (its base chance is 0): only a
    /// dealing agreed sends it, for that town alone.
    /// </summary>
    public static class DealingFestival
    {
        public const string DefName = "Rimfolk_Mission_Festival";

        /// <summary>While set, the dealing the festival is offered for (set and cleared around one firing; not saved).</summary>
        [ThreadStatic] internal static Dealing making;

        /// <summary>The longest caravan journey a festival is worth, in days.</summary>
        public const float MaxDays = 12f;

        /// <summary>The festival's journey could be offered for this town now: the mission there, the town within a caravan's reach, enough of the colony's grown people.</summary>
        public static bool CanOffer(Faction f, Settlement town, Map home)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(DefName);
            if (def == null || f == null || town == null || home == null || Diplomacy.Hostile(f)) return false;
            return MissionPeoples.TravelDays(home, town) <= MaxDays && StorytellerEvents.Adults(home).Count() >= 3;
        }

        /// <summary>The town's festival offered to the colony: the mission for its people alone. False when it can't be now.</summary>
        public static bool Invite(Dealing d, Map home)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(DefName);
            if (def == null || d?.people == null || home == null) return false;
            MissionPeoples.Only = d.people;
            making = d;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, home);
                parms.forced = true;
                parms.faction = d.people;
                return def.Worker.CanFireNow(parms) && def.Worker.TryExecute(parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dealings: the festival's invitation", e);
                return false;
            }
            finally
            {
                MissionPeoples.Only = null;
                making = null;
            }
        }
    }

    /// <summary>A town's festival: the invitation's own journey and stay, at the dealing's town, its entertainers the colony's best at the arts.</summary>
    public class HappeningWorker_MissionFestival : HappeningWorker_MissionInvitation
    {
        protected override string Kind => "festival";

        protected override float RestDays => 0f;

        protected override float MaxTravelDays => DealingFestival.MaxDays;

        protected override bool Possible(Map map) => DealingFestival.making != null && base.Possible(map);

        protected override bool Setup(Mission m, HappeningContext ctx, HappeningExtension ext)
        {
            var d = DealingFestival.making;
            if (d == null || !base.Setup(m, ctx, ext) || m.faction != d.people) return false;
            var map = ctx.map;
            var town = d.Town;
            if (town != null && town.HasName && town.Faction == d.people)
            {
                m.place = town.Name;
                float travel = MissionPeoples.TravelDays(map, town);
                m.tale = TextUtil.DaysString(Mathf.RoundToInt(Mathf.Max(0.3f, travel) * GenDate.TicksPerDay));
                m.days = Mathf.Ceil(travel * 1.5f + 3f);
                // Who gives it: their head there as the ledger knows them, else someone of theirs named.
                m.host = InviteForms.Host(town, out bool royal);
                m.royalHost = royal;
                if (!m.host.NullOrEmpty()) m.name = m.host;
            }
            m.variant = d.kind == DealKind.Reply ? "invited" : "entertainers";
            m.rite = null;
            m.riteOfFaith = false;
            // The colony's best at the arts leads the party (the best talker when nobody has the knack).
            m.a = VarietyUtil.Best(map, SkillDefOf.Artistic) ?? m.a;
            Fill(m, ctx);
            return true;
        }
    }
}
