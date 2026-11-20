using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The seat whose work it is speaks in the question (the content update's A2), and the weight of office's hard call made
    /// real (A21). A council question about a seat's work (DilemmaDef.seat; a seasonal custom is the keeper of days')
    /// names the seat's holder when they are at home and neither A nor B: lines tagged [seat] (and [seat:Reeve]) give them
    /// a voice in the opening and the outcome ({HOLDER_...}, {OFFICE} the seat in the colony's age), and a question with no
    /// opening of its own for the seat has the holder's word after it (Rimfolk_Pool_SeatWord). When it is answered, it
    /// counts toward the seat's record that the next election reads (the record Council_SeatCalls, read since the promise
    /// in Offices/SeatRecords.cs), and it is the holder's hard call if their "weight of office" story waits for one. The
    /// story's other calls come from a decree they backed declared while someone hates it (<see cref="Notify_Decree"/>)
    /// and from answering for a promise at supper (Offices/SeatHalfway.cs). Answers to "Systems talk to each other" and
    /// the offices' setting.
    /// </summary>
    public static class SeatCalls
    {
        /// <summary>Questions of the seat's work a holder spoke in (Defs/Misc/Council_Records.xml).</summary>
        public const string Record = "Council_SeatCalls";

        /// <summary>The holder's word after a question with none of its own for the seat.</summary>
        public const string WordPool = "Rimfolk_Pool_SeatWord";

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static bool On => Interplay.On && S.offices;

        /// <summary>How many questions of their seat's work someone has spoken in (the game's record of it).</summary>
        public static int Count(Pawn p)
        {
            var def = DefDatabase<RecordDef>.GetNamedSilentFail(Record);
            if (def == null || p?.records == null) return 0;
            try
            {
                return (int)p.records.GetValue(def);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("seat calls record", e);
                return 0;
            }
        }

        // ---------------- The holder in the question ----------------

        /// <summary>
        /// The holder of the seat whose work the question is about: of the colony of its map, at home on that map, up, and
        /// neither A, B nor the stranger. Null for a question of no seat's work, or with nobody to speak for it.
        /// </summary>
        public static Pawn HolderFor(DilemmaContext ctx)
        {
            if (!On || ctx?.def == null || ctx.map == null) return null;
            var office = ctx.def.SeatOffice;
            if (office == null) return null;
            var h = Interplay.Officer(office.defName, ctx.map);
            if (h == null || h == ctx.a || h == ctx.b || h == ctx.stranger || h.Dead || !h.Spawned || h.Map != ctx.map || h.Downed) return null;
            return h;
        }

        /// <summary>[seat] and [seat:Physician] when the question has its seat's holder in it.</summary>
        public static void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var office = ctx?.def?.SeatOffice;
            if (ctx?.holder == null || office == null || tags == null) return;
            tags.Add("seat");
            tags.Add(SeatTag(office));
        }

        public static string SeatTag(OfficeDef office) => "seat:" + office.defName.Replace("Council_Office_", "");

        /// <summary>The question has an opening of its own for the seat that fits now (a line tagged [seat] or [seat:...]).</summary>
        public static bool OwnWords(List<string> texts, HashSet<string> tags) =>
            texts != null && texts.Any(t => HasSeatTag(t) && Flavor.Match(t, tags) >= 0);

        private static bool HasSeatTag(string line)
        {
            Flavor.Strip(line, out var tags);
            return tags != null && tags.Any(t => t == "seat" || t.StartsWith("seat:"));
        }

        /// <summary>The question's opening with the holder's word after it (placeholders left for the letter to fill).</summary>
        public static string WithWord(string text, DilemmaContext ctx, HashSet<string> tags)
        {
            var office = ctx?.def?.SeatOffice;
            if (ctx?.holder == null || office == null) return text;
            string word = Flavor.FromPool(WordPool, ctx.holder, null, null, "seat", SeatTag(office));
            return word.NullOrEmpty() ? text : text + "\n\n" + word;
        }

        // ---------------- Answered ----------------

        /// <summary>
        /// A question with its seat's holder in it is answered: one more for the seat's record, and the holder's hard call
        /// if their story of the weight of office waits for one.
        /// </summary>
        public static void Answered(DilemmaContext ctx)
        {
            var h = ctx?.holder;
            var office = ctx?.def?.SeatOffice;
            if (!On || h == null || office == null || h.Dead || !Interplay.Holds(h, office.defName)) return;
            try
            {
                CouncilRecords.Add(h, Record);
                Call(h, SeatMath.CallQuestion, ctx.def.label, office);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("seat question answered", e);
            }
        }

        /// <summary>
        /// A real call for a holder newly seated (the weight of office, A21): noted on their story while it waits for one
        /// (its tag says which kind, {CALL} what it was), and its gate opens at the next hour. A question's call counts
        /// only for the seat the story is about.
        /// </summary>
        public static bool Call(Pawn holder, string kind, string what, OfficeDef office = null)
        {
            var arc = ArcTracker.Instance?.Of(holder);
            if (arc?.def == null || !(arc.def.Worker is ArcWorker_Office) || arc.stage > SeatMath.CallStage || SeatMath.IsCall(arc.tag)) return false;
            if (office != null && arc.key != office.defName) return false;
            arc.tag = kind;
            arc.call = what;
            return true;
        }

        /// <summary>
        /// A decree declared (DecreeTracker.Enact): a holder of the colony's seats who asked for it or likes it, while
        /// someone of the colony at home hates it, has made a hard call by backing it.
        /// </summary>
        public static void Notify_Decree(DecreeTracker t, DecreeDef def, Pawn asker)
        {
            if (!On || def == null) return;
            try
            {
                var council = t?.colony?.offices ?? CouncilGameComponent.Instance?.offices;
                var home = t?.colony?.Map ?? MapFacts.BusiestHome;
                if (council == null || home == null) return;
                var people = home.mapPawns.FreeColonistsSpawned.Where(p => Interplay.Ours(p) && (t == null || t.Ours(p))).ToList();
                foreach (var office in OfficeTracker.Offices)
                    foreach (var h in council.HoldersOf(office))
                    {
                        if (h == null || h.Dead) continue;
                        bool backed = h == asker || def.ReactionOf(h) >= DecreeReaction.Like;
                        if (!backed || !people.Any(p => p != h && def.ReactionOf(p) == DecreeReaction.Hate)) continue;
                        Call(h, SeatMath.CallDecree, def.label);
                    }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("seat call: decree", e);
            }
        }
    }
}
