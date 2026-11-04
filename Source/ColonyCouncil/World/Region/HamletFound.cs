using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    // The hamlet stands (docs/design/stage1-region.md, item 6; decision 9: a town of the settlers' own people): the game's
    // own settlement of their people on the place, named by their naming rule; on the ledger as a hamlet, founded now, its
    // fortune from 25 (12 when they came alone), its founder the settlers' leader (its head among the notables), its
    // founder's line and one more founding family; a Fealty tie from the hamlet to the colony (key "protection", open); the
    // history's milestone; their people's memory; the colonists who walked them out remembered by name (World/ColonistMemory.cs).
    public static partial class Hamlets
    {
        /// <summary>The settlers found their hamlet now (the first fires lit after a night at the place, or the road alone done).</summary>
        internal static Settlement Found(Map home, HamletHome h, bool escorted)
        {
            if (home == null || h == null || !HamletMath.OnTheirWay(h.stage)) return null;
            var people = h.people;
            if (!CanFound(people))
            {
                End(home, h, HamletEnd.Gone, null);
                return null;
            }
            var tile = TileOf(h);
            // Someone else settled the place meanwhile: they settle the nearest fitting place instead.
            if (!tile.Valid || Find.WorldObjects.ObjectsAt(tile).Any(o => !(o is Caravan)))
            {
                tile = FindTile(home, people);
                if (!tile.Valid)
                {
                    End(home, h, HamletEnd.Gone, null);
                    return null;
                }
                h.tile = Refs.Of(tile);
            }
            var town = MakeTown(people, tile);
            if (town == null) return null;
            int now = Now;
            var leader = h.leader != null && !h.leader.Dead && !h.leader.Discarded ? h.leader : h.Living.FirstOrDefault(p => !p.DevelopmentalStage.Juvenile());
            h.leader = leader;
            h.town = town.ID;
            h.name = town.Name;
            h.founded = now;
            h.stage = HamletStage.Standing;
            h.count++;
            var rec = WorldLedger.TownFor(town.ID, town.Name, people, true);
            if (rec != null)
            {
                rec.kind = TownKind.Hamlet;
                rec.founded = now;
                rec.founder = leader != null ? WorldLedger.PersonOf(leader) : Ref.None;
                rec.mother = h.from;
                rec.motherName = h.fromName;
                rec.why = WhyHere(h, tile);
                if (RegionBook.FortunesOn)
                {
                    rec.prosperity = escorted ? HamletMath.Start : HamletMath.AloneStart;
                    rec.movedAt = now;
                    rec.cause = "CC_Hamlet_Cause_Founded".Translate().Resolve();
                    rec.causeDir = 0;
                    rec.causeAt = now;
                    rec.lowSince = -1;
                }
                WorldLedger.TownChanged();
            }
            if (leader != null) WorldLedger.Keep(leader, KeepFor.Founding, false);
            Safe("its families", () => Families(town, h, leader));
            Safe("its head", () => NotableBook.StaffHamlet(town, leader, HamletWords.NoteFounded(town)));
            Safe("its tie", () =>
            {
                var t = WorldLedger.AddTie(Tie.Kind.Fealty, Ref.Town(town.ID), Refs.Colony(home), new TieTerms { key = "protection", about = Refs.Of(people) }, Tie.Never, "hamlet");
                h.tie = t?.id ?? -1;
            });
            var walked = escorted ? h.escorts.Where(p => p != null && !p.Dead && p.Faction == Faction.OfPlayer).ToList() : new List<Pawn>();
            ReleaseSettlers(h);
            RegionTowns.Invalidate();
            NotableBook.Changed();
            HistoryOf(HamletMath.KindFounded, home, h, HamletWords.HistoryFounded(h, home));
            FactionMemory.Note(people, HamletMath.DeedProtected, town.Name, escorted ? 0 : 8);
            // Those of the colony who walked the settlers out, remembered by name by their people (World/ColonistMemory.cs).
            if (walked.Count > 0) Safe("those who walked them out", () => ColonistMemory.WalkedSettlers(people, walked));
            HamletWords.TellFounded(home, h, town, escorted, walked);
            return town;
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: " + what, e);
            }
        }

        /// <summary>The hamlet as the game's own town of their people on the place, named by their naming rule; null when it can't be made.</summary>
        private static Settlement MakeTown(Faction people, PlanetTile tile)
        {
            try
            {
                var town = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                town.SetFaction(people);
                town.Tile = tile;
                town.Name = SettlementNameGenerator.GenerateSettlementName(town, people.def.settlementNameMaker);
                Find.WorldObjects.Add(town);
                return town;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: its town made", e);
                return null;
            }
        }

        /// <summary>
        /// Its founding families: the founder's line, with the settlers who share their name by their first names, and one
        /// more family (those of the settlers with another name, else a family that came with them, known by name).
        /// </summary>
        private static void Families(Settlement town, HamletHome h, Pawn founder)
        {
            var people = town.Faction;
            bool surnames = founder == null || founder.Name is NameTriple tri0 && !tri0.Last.NullOrEmpty();
            var living = h.Living;
            string line = founder == null ? null : surnames ? ((NameTriple)founder.Name).Last : founder.LabelShort;
            if (!line.NullOrEmpty())
            {
                var kin = living.Where(p => p != founder && (!surnames || (p.Name as NameTriple)?.Last == line)).Select(FirstName).Where(n => !n.NullOrEmpty()).Distinct().ToList();
                WorldLedger.AddHouse(town.ID, people, line, WorldLedger.PersonOf(founder), kin, true, !surnames);
            }
            var others = surnames ? living.Where(p => (p.Name as NameTriple)?.Last is string l && !l.NullOrEmpty() && l != line).ToList() : new List<Pawn>();
            string other = others.Count > 0 ? ((NameTriple)others[0].Name).Last : surnames ? Surname() : null;
            if (other.NullOrEmpty() || other == line) return;
            var members = others.Where(p => ((NameTriple)p.Name).Last == other).Select(FirstName).Where(n => !n.NullOrEmpty()).ToList();
            for (int i = 0; i < 4 && members.Count < 3; i++)
            {
                string n = FirstNameFromBank();
                if (!n.NullOrEmpty() && !members.Contains(n)) members.Add(n);
            }
            WorldLedger.AddHouse(town.ID, people, other, Ref.None, members, true);
        }

        private static string FirstName(Pawn p) => p?.Name is NameTriple t ? (t.Nick.NullOrEmpty() ? t.First : t.Nick) : p?.LabelShort;

        private static Gender AnyGender => Rand.Bool ? Gender.Male : Gender.Female;

        /// <summary>A family name from the game's own bank of names, or null.</summary>
        internal static string Surname()
        {
            try
            {
                return PawnNameDatabaseShuffled.BankOf(PawnNameCategory.HumanStandard).GetName(PawnNameSlot.Last, AnyGender, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: a family's name", e);
                return null;
            }
        }

        private static string FirstNameFromBank()
        {
            try
            {
                return PawnNameDatabaseShuffled.BankOf(PawnNameCategory.HumanStandard).GetName(PawnNameSlot.First, AnyGender, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hamlets: a family member's name", e);
                return null;
            }
        }
    }
}
