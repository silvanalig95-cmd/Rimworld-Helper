using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Where a click on a name in diplomacy's rows goes: one helper, pointed at the cards (UI/Cards/Cards.cs). A people, a
    /// town, a notable, anyone, a tie or a ledger ref opens its card where the player is (the region window's drawer, else
    /// the small floating card); a caravan shows on the region map; a letter of the game's opens in the letter stack.
    /// </summary>
    public static class DiplomacyLinks
    {
        /// <summary>Opens what <paramref name="thing"/> names (a Faction, Settlement, Notable, Pawn, Caravan, Letter, Tie, Ref or ThingRef); false when there is nothing to open.</summary>
        public static bool Open(object thing)
        {
            try
            {
                if (!Opens(thing)) return false;
                switch (thing)
                {
                    case Settlement s:
                        Cards.Open(ThingRefs.Of(s));
                        return true;
                    case Faction f:
                        Cards.Open(ThingRefs.Of(f));
                        return true;
                    case Notable n:
                        Cards.Open(ThingRefs.Of(n));
                        return true;
                    case Caravan c:
                        Cards.ShowOnMap(c);
                        return true;
                    case Pawn p:
                        Cards.Open(ThingRefs.Of(p));
                        return true;
                    case Letter l:
                        l.OpenLetter();
                        return true;
                    case Tie t:
                        Cards.Open(ThingRefs.Of(t));
                        return true;
                    case Ref r:
                        Cards.Open(ThingRef.From(r));
                        return true;
                    case ThingRef tr:
                        Cards.Open(tr);
                        return true;
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
                case Faction f: return !f.IsPlayer;
                case Notable n: return !n.Person.IsNone;
                case Caravan c: return !c.Destroyed;
                case Pawn p: return !p.Discarded;
                case Letter l: return Find.LetterStack.LettersListForReading.Contains(l);
                case Tie t: return !t.IsView && ThingRefs.Exists(ThingRefs.Of(t));
                case Ref r: return ThingRefs.Exists(ThingRef.From(r));
                case ThingRef tr: return ThingRefs.Exists(tr);
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
    }
}
