using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Marks from the colony's own life (A22 of docs/content-update-proposals.md, the player's go of 20 November 2026):
    /// five turning points read by each one's nature as the other marks are (Defs/PersonaDefs/Rimfolk_PersonaMarks_ColonyLife.xml):
    /// a life goal reached, an idea of theirs built (the ambition they proposed reached), a custom they first asked for
    /// become a tradition, a year in a seat with the promise kept, and a personal story that ended well. One line each in
    /// the code that already tells of the thing (Aspirations, Ambitions, the council's customs, the seats' records, the
    /// stories); the marks write their own journal lines.
    /// </summary>
    public static class LifeMarks
    {
        private static void Give(Pawn p, string family, string label)
        {
            if (p == null || p.Dead) return;
            try
            {
                PersonaMarks.Give(p, family, null, label);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony life, part 2: a mark " + family, e);
            }
        }

        /// <summary>They reached their life goal (Aspirations.Fulfil).</summary>
        public static void GoalReached(Pawn p, string goal) => Give(p, "GoalReached", goal);

        /// <summary>The ambition they proposed was reached (Ambitions.Complete).</summary>
        public static void IdeaBuilt(Pawn proposer, string ambition) => Give(proposer, "IdeaBuilt", ambition);

        /// <summary>A custom became a tradition (the council's own customs, three years kept): its first asker, if still here.</summary>
        public static void CustomBecameTradition(DilemmaDef def, string label)
        {
            var asker = LifeVarietyBook.Instance?.FirstAsker(def);
            if (asker != null) Give(asker, "CustomTradition", label);
        }

        /// <summary>A year in a seat, and the promise they won it on judged kept (SeatRecords.Judged).</summary>
        public static void SeatYearKept(Pawn holder, OfficeDef office) => Give(holder, "SeatYearKept", office?.Name);

        /// <summary>A personal story of theirs came to its good end (ArcTracker.Complete).</summary>
        public static void StoryEndedWell(Pawn p, string story) => Give(p, "StoryEndedWell", story);
    }

    /// <summary>
    /// Deeds and names of the council's life (A23): kept a promise in the seat and broke one (the promise judged at the
    /// yearly election, SeatRecords.Judged), spoke up for someone before the council (the one named objecting when the
    /// council is asked to send someone away; stepping down from a seat is A3's own deed, CouncilLife/StepDown.cs); and the records of days in the seats that had none (the quartermaster's, the reeve's,
    /// the herdmaster's, the steward's, the elders') and of promises in a seat judged kept, for the names that grow on them
    /// (Defs/EpithetDefs/Rimfolk_Epithets_Council.xml: Old Keys, Old Furrow, the Drover, the Host, the Old Voice, Word-Kept).
    /// </summary>
    public static class LifeDeeds
    {
        public const string PromisesKeptRecord = "Council_SeatPromisesKept";

        /// <summary>A holder's promise judged at the election: kept or broken, a deed for gossip; kept, the record and the mark.</summary>
        public static void Judged(Pawn holder, OfficeDef office, bool kept)
        {
            if (holder == null || office == null) return;
            try
            {
                Deeds.Record(kept ? "Rimfolk_Deed_SeatPromiseKept" : "Rimfolk_Deed_SeatPromiseBroken", holder, null, -1f, null, office.Name, null, false);
                if (!kept) return;
                CouncilRecords.Add(holder, PromisesKeptRecord);
                LifeMarks.SeatYearKept(holder, office);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony life, part 2: a promise judged", e);
            }
        }

        /// <summary>Named in the council's letter objecting to <paramref name="forWhom"/> being sent away: spoke up for them.</summary>
        public static void SpokeUp(Pawn speaker, Pawn forWhom)
        {
            if (speaker == null || forWhom == null || speaker == forWhom) return;
            try
            {
                Deeds.Record("Rimfolk_Deed_SpokeUp", speaker, forWhom, -1f, null, null, null, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony life, part 2: spoke up", e);
            }
        }


        /// <summary>Once a day with the names (NameRecords.Daily): a day more in each of the seats that had no record of days.</summary>
        public static void SeatDays(OfficeTracker offices)
        {
            if (offices == null) return;
            foreach (var seat in CouncilNameMath.Seats)
            {
                string record = CouncilNameMath.DaysRecord(seat);
                if (record == null) continue;
                foreach (var p in offices.HoldersOf(seat)) CouncilRecords.Add(p, record);
            }
        }
    }
}
