using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: another mod's peoples Rimfolk dresses (Debug actions, "Rimfolk", "Peoples of other mods: ..."): four new
    /// pawns of each of their kinds described in the log (role, look, clothes and colours, weapon, class or psylink, hair and
    /// tattoos), not kept; or one of each here now as visitors for half a day.
    /// </summary>
    public static class ForeignPeoplesDebug
    {
        private static void Say(string text) => Messages.Message("Peoples of other mods: " + text, MessageTypeDefOf.SilentInput, false);

        private static IEnumerable<Faction> Factions(bool atPeace) =>
            Find.FactionManager.AllFactionsListForReading.Where(f => f != null && !f.defeated && ForeignPeoples.Has(f) && (!atPeace || !f.HostileTo(Faction.OfPlayer)));

        /// <summary>A new pawn of theirs, with its role and look, its hair and tattoos (PeoplesDebug.Describe for the rest).</summary>
        private static string Describe(Pawn p)
        {
            var role = ForeignPeoples.RoleOf(ForeignPeoples.Of(p.Faction), p.kindDef);
            var sb = new StringBuilder(PeoplesDebug.Describe(p));
            sb.Append("; role " + (role?.name ?? "none"));
            if (p.story?.hairDef != null) sb.Append("; hair " + p.story.hairDef.defName);
            if (p.style != null)
            {
                if (p.style.FaceTattoo != null && p.style.FaceTattoo.defName != "NoTattoo_Face") sb.Append("; face tattoo " + p.style.FaceTattoo.defName);
                if (p.style.BodyTattoo != null && p.style.BodyTattoo.defName != "NoTattoo_Body") sb.Append("; body tattoo " + p.style.BodyTattoo.defName);
            }
            return sb.ToString();
        }

        [DebugAction("Rimfolk", "Peoples of other mods: their gear, looks and gifts (log)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGear()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var f in Factions(false))
            {
                var people = f;
                options.Add(new FloatMenuOption(people.Name, () =>
                {
                    var sb = new StringBuilder("[Rimfolk] Peoples of other mods: four new pawns of each kind of " + people.Name + " (" + people.def.defName + "), not kept:");
                    sb.AppendLine();
                    foreach (var kind in PeoplesDebug.KindsOf(people.def).Where(k => k.RaceProps?.Humanlike == true))
                        for (int i = 0; i < 4; i++)
                        {
                            var p = PeoplesDebug.NewOf(kind, people, map);
                            if (p == null) continue;
                            sb.AppendLine(Describe(p));
                            Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
                        }
                    Log.Message(sb.ToString().TrimEnd());
                    Say("listed in the log.");
                }));
            }
            if (options.Count == 0) Say("no people of another mod that Rimfolk dresses is in this world.");
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples of other mods: one of each of their kinds here now, as visitors (testing)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGroup()
        {
            var map = Find.CurrentMap;
            var at = UI.MouseCell();
            var options = new List<FloatMenuOption>();
            foreach (var f in Factions(true))
            {
                var people = f;
                options.Add(new FloatMenuOption(people.Name, () =>
                {
                    var pawns = new List<Pawn>();
                    var sb = new StringBuilder("[Rimfolk] Peoples of other mods: one of each kind of " + people.Name + " at " + at + ":");
                    sb.AppendLine();
                    foreach (var kind in PeoplesDebug.KindsOf(people.def).Where(k => k.RaceProps?.Humanlike == true))
                    {
                        var p = PeoplesDebug.NewOf(kind, people, map);
                        if (p == null) continue;
                        if (!CellFinder.TryFindRandomCellNear(at, map, 6, c => c.Standable(map) && !c.Fogged(map), out var cell)) cell = at;
                        GenSpawn.Spawn(p, cell, map);
                        pawns.Add(p);
                        sb.AppendLine(Describe(p));
                    }
                    if (pawns.Count == 0)
                    {
                        Say("no one could be made.");
                        return;
                    }
                    LordMaker.MakeNewLord(people, new LordJob_VisitOnErrand(people, at, GenDate.TicksPerDay / 2), map, pawns);
                    Log.Message(sb.ToString().TrimEnd());
                    Say(pawns.Count + " of " + people.Name + " here as visitors for half a day (each described in the log).");
                }));
            }
            if (options.Count == 0) Say("no people of another mod that Rimfolk dresses is at peace with the colony in this world.");
            else Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
