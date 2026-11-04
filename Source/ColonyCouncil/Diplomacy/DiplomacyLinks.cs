using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where a click on a name in diplomacy's rows goes: one helper, so the interface's redesign can point every link
    /// somewhere new in one place. For now it opens what exists: a people or a town on the region map with its panel, a
    /// notable's town there (or the notable themselves when they are on the map), a colonist or anyone on a map where they
    /// stand, a caravan on the world map, a letter in the letter stack.
    /// </summary>
    public static class DiplomacyLinks
    {
        /// <summary>Opens what <paramref name="thing"/> names (a Faction, Settlement, Notable, Pawn, Caravan, Letter or Tie); false when there is nothing to open.</summary>
        public static bool Open(object thing)
        {
            try
            {
                switch (thing)
                {
                    case null:
                        return false;
                    case Settlement s:
                        if (s.Destroyed) return false;
                        MainTabWindow_World.ShowOnMap(s);
                        return true;
                    case Faction f:
                    {
                        var town = TownOf(f);
                        if (town == null)
                        {
                            Pages.Open("Peoples");
                            return true;
                        }
                        MainTabWindow_World.ShowOnMap(town);
                        return true;
                    }
                    case Notable n:
                        if (n.pawn != null && n.pawn.Spawned) return Open(n.pawn);
                        var home = NotableBook.TownOf(n.town);
                        return home != null ? Open(home) : Open(n.faction);
                    case Pawn p:
                        return Jump(p);
                    case Caravan c:
                        Find.MainTabsRoot.EscapeCurrentTab(false);
                        CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(c));
                        return true;
                    case Letter l:
                        if (!Find.LetterStack.LettersListForReading.Contains(l)) return false;
                        l.OpenLetter();
                        return true;
                    case Tie t:
                        return Open(FavoursOwed.PeopleOf(t));
                    case Ref r:
                        return Open(Of(r));
                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("diplomacy: a link", e);
                return false;
            }
        }

        /// <summary>Whether a click on it would go anywhere (rows draw a link only then).</summary>
        public static bool Opens(object thing)
        {
            switch (thing)
            {
                case null: return false;
                case Settlement s: return !s.Destroyed;
                case Faction f: return true;
                case Notable n: return n.pawn != null && n.pawn.Spawned || NotableBook.TownOf(n.town) != null || n.faction != null;
                case Pawn p: return !p.Dead && (p.Spawned || p.GetCaravan() != null);
                case Caravan _: return true;
                case Letter l: return Find.LetterStack.LettersListForReading.Contains(l);
                case Tie t: return FavoursOwed.PeopleOf(t) != null;
                case Ref r: return Of(r) != null;
                default: return false;
            }
        }

        /// <summary>What a ledger ref names, while the game has it: a people, a town, a person's notable record or pawn.</summary>
        public static object Of(Ref r)
        {
            switch (r.kind)
            {
                case RefKind.People: return Refs.Faction(r);
                case RefKind.Town: return Refs.Town(r);
                case RefKind.Person:
                {
                    var n = NotableBook.Instance?.notables.Find(x => x != null && x.Person == r);
                    if (n != null) return n;
                    return WorldLedger.PawnOf(r);
                }
                default: return null;
            }
        }

        private static Settlement TownOf(Faction f)
        {
            if (f == null) return null;
            var home = Homes.Seat;
            return home != null ? WorldSituationWorker.NearestTown(f, home) : NotableBook.SeatOf(f);
        }

        private static bool Jump(Pawn p)
        {
            if (p == null || p.Dead) return false;
            if (p.Spawned)
            {
                Find.MainTabsRoot.EscapeCurrentTab(false);
                CameraJumper.TryJumpAndSelect(p);
                return true;
            }
            var c = p.GetCaravan();
            if (c != null) return Open(c);
            return false;
        }
    }
}
