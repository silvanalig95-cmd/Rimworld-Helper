using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Any other business (the content update's A6): once the ambitions have been put at the seasonal meeting, each open
    /// petition of the colony is read out, the oldest first (four at most): a decree asked for or asked to be lifted (the
    /// existing DecreeRequest) and a petition of wishes (several names on one want, or a want the colony can back
    /// together). Its asker reads it (the chair for one who isn't there), one line; someone there says aye or no; those
    /// present are counted by how they feel about it (the decree's reaction to them, or wanting the same thing, or thinking
    /// well of whoever asked); anyone there who wants the same thing joins it (the existing co-signing, or their own want
    /// joining the petition). The tally stays on the petition ("backed at the meeting by 5 of 7"): on the Decrees page, in
    /// This week, and in the meeting's letter. The council still decides from the page, as before.
    /// </summary>
    public static class MeetingBusiness
    {
        private static readonly Color SpeechColor = new Color(0.7f, 0.85f, 1f);

        /// <summary>The ambitions are put, so the business can come: every pitch made or read out, and the meeting well under way.</summary>
        private const float From = 0.4f;

        /// <summary>At most this many join one petition at the meeting.</summary>
        private const int MaxJoin = 2;

        private static bool On => Interplay.On && ColonyCouncilMod.Settings.offices;

        // ---------------- The petitions of a meeting ----------------

        /// <summary>One open petition: a decree asked for or to be lifted, or the want that leads a petition of wishes.</summary>
        private sealed class Petition
        {
            public DecreeRequest request;
            public Wish wish;

            public int Tick => request?.tick ?? wish?.createdTick ?? 0;
            public int Heard => request?.heardTick ?? wish?.heardTick ?? -1;
            public Pawn Asker => request?.pawn ?? wish?.pawn;
        }

        /// <summary>The colony's open petitions, oldest first.</summary>
        private static List<Petition> Open(Map map)
        {
            var list = new List<Petition>();
            var comp = CouncilGameComponent.Instance;
            var decrees = DecreeTracker.For(map);
            if (decrees != null)
                foreach (var r in decrees.requests)
                    if (r?.def != null && r.pawn != null && !r.pawn.Dead)
                        list.Add(new Petition { request = r });
            if (comp != null && ColonyCouncilMod.Settings.enableWishes)
            {
                var colony = ColonyState.For(map);
                foreach (var w in comp.OpenWishes.ToList())
                {
                    if (w?.def == null || w.pawn == null || !comp.LeadsPetition(w) || (colony != null && !colony.Has(w.pawn))) continue;
                    if (comp.IsPetition(w) || w.coSigners.Count > 0 || w.def.allowPetition) list.Add(new Petition { wish = w });
                }
            }
            return list.OrderBy(p => p.Tick).ToList();
        }

        /// <summary>
        /// The meeting's next word on its business (CouncilMeeting.Talk), once the ambitions are put: the next petition not
        /// yet read at this meeting, read, answered and counted. True when something was said.
        /// </summary>
        public static bool Next(CouncilMeeting m, List<Pawn> here)
        {
            if (!On || m?.map == null || here.NullOrEmpty() || m.Elapsed < From) return false;
            if (m.pitches.Any(p => !p.spoken && !p.readOut)) return false;
            try
            {
                var open = Open(m.map);
                if (open.Count(p => p.Heard >= m.startedTick) >= SeatMath.MaxRead) return false;
                var next = open.FirstOrDefault(p => p.Heard < m.startedTick);
                if (next == null) return false;
                Read(m.chair, next, here);
                return true;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("meeting: any other business", e);
                return false;
            }
        }

        private static void Read(Pawn chair, Petition pet, List<Pawn> here)
        {
            int now = Find.TickManager.TicksGame;
            var comp = CouncilGameComponent.Instance;
            var asker = pet.Asker;
            string what = pet.request != null ? pet.request.def.label : pet.wish.LabelInline;
            string kind = pet.request == null ? "wish" : pet.request.repeal ? "repeal" : "decree";
            // together: read out at the meeting itself, by its asker or (for one who isn't there) by the chair, to all there.
            var reader = here.Contains(asker) ? asker : chair != null && here.Contains(chair) && chair != asker ? chair : here.FirstOrDefault(p => p != asker);
            if (reader != null)
            {
                string pool = reader == asker ? "Rimfolk_Pool_MeetingPetition" : "Rimfolk_Pool_MeetingPetitionFor";
                Say(reader, pool, asker, what, kind);
            }
            // Those there say aye or no by how they feel about it.
            int ayes = 0;
            Pawn aye = null, nay = null;
            var join = new List<Pawn>();
            foreach (var p in here)
            {
                bool yes;
                if (pet.request != null)
                    yes = p == asker || SeatMath.AyeToDecree((int)pet.request.def.ReactionOf(p) - (int)DecreeReaction.Neutral, pet.request.repeal);
                else
                {
                    bool inIt = pet.wish.Involves(p) || (comp?.PetitionPeople(pet.wish).Contains(p) ?? false);
                    bool same = !inIt && comp != null && comp.WantsSame(pet.wish, p);
                    if (same) join.Add(p);
                    int opinion = p.relations != null && asker != null && p != asker ? p.relations.OpinionOf(asker) : 0;
                    yes = SeatMath.AyeToWish(inIt, same, opinion);
                }
                if (yes) ayes++;
                if (p == asker || p == reader) continue;
                if (yes && aye == null) aye = p;
                if (!yes && nay == null) nay = p;
            }
            // One voice from the room: a no, where there is one, more often than an aye.
            var voice = nay != null && (aye == null || Rand.Chance(0.6f)) ? nay : aye;
            if (voice != null) Say(voice, voice == nay ? "Rimfolk_Pool_MeetingNay" : "Rimfolk_Pool_MeetingAye", asker, what, kind);
            if (pet.request != null)
            {
                pet.request.heardTick = now;
                pet.request.ayes = ayes;
                pet.request.present = here.Count;
            }
            else
            {
                pet.wish.heardTick = now;
                pet.wish.ayes = ayes;
                pet.wish.present = here.Count;
                // Anyone there who wants the same joins it, as anyone may who wants it.
                foreach (var p in join.Take(MaxJoin)) comp?.JoinAtMeeting(pet.wish, p);
            }
        }

        private static void Say(Pawn speaker, string pool, Pawn asker, string what, string kind)
        {
            string line = Flavor.FromPool(pool, speaker, null, null, kind);
            if (line.NullOrEmpty()) return;
            line = line.Formatted(speaker.Named("PAWN"), (asker ?? speaker).Named("OTHER"), (what ?? "").Named("WHAT")).Resolve();
            CouncilGameComponent.Speak(speaker, line.CapitalizeFirst(), SpeechColor);
        }

        /// <summary>
        /// Dev mode (Offices/SeatDebug.cs): the colony's open petitions read out now as at a meeting, with everyone of the
        /// colony up and about on this map in the room. How many were read.
        /// </summary>
        internal static int DebugReadAll(Map map)
        {
            if (map == null) return 0;
            var here = map.mapPawns.FreeColonistsSpawned.Where(p => MeetingAttendance.IsMember(p) && Together.Able(p)).ToList();
            if (here.Count == 0) return 0;
            var chair = OfficeTracker.For(map)?.Speaker;
            int n = 0;
            foreach (var p in Open(map).Take(SeatMath.MaxRead))
            {
                Read(chair, p, here);
                n++;
            }
            return n;
        }

        // ---------------- The tally, where the petition is shown ----------------

        /// <summary>"Backed at the meeting by 5 of 7." (or "not backed"), or null when it hasn't been read at a meeting.</summary>
        public static string Tally(int heardTick, int ayes, int present)
        {
            if (heardTick < 0 || present <= 0) return null;
            return (SeatMath.Backed(ayes, present) ? "CC_Business_Backed" : "CC_Business_NotBacked").Translate(ayes.Named("AYES"), present.Named("PRESENT")).Resolve();
        }

        public static string Tally(DecreeRequest r) => r == null ? null : Tally(r.heardTick, r.ayes, r.present);

        public static string Tally(Wish w) => w == null ? null : Tally(w.heardTick, w.ayes, w.present);

        /// <summary>The letter's words for one petition: a wish (0), a decree (1) or lifting one (2); its asker away or there; backed or not.</summary>
        private static string LetterKey(int kind, bool absent, bool backed)
        {
            switch (kind)
            {
                case 1:
                    return absent ? (backed ? "CC_Business_LetterDecreeAbsent_Backed" : "CC_Business_LetterDecreeAbsent_Not")
                        : backed ? "CC_Business_LetterDecree_Backed" : "CC_Business_LetterDecree_Not";
                case 2:
                    return absent ? (backed ? "CC_Business_LetterRepealAbsent_Backed" : "CC_Business_LetterRepealAbsent_Not")
                        : backed ? "CC_Business_LetterRepeal_Backed" : "CC_Business_LetterRepeal_Not";
                default:
                    return absent ? (backed ? "CC_Business_LetterWishAbsent_Backed" : "CC_Business_LetterWishAbsent_Not")
                        : backed ? "CC_Business_LetterWish_Backed" : "CC_Business_LetterWish_Not";
            }
        }

        /// <summary>
        /// The meeting letter's paragraph on its other business: each petition read out at this meeting, who read it and how
        /// the room took it. Null when nothing was read.
        /// </summary>
        public static string LetterParagraph(CouncilMeeting m)
        {
            if (m?.map == null || m.startedTick < 0) return null;
            try
            {
                var lines = new List<string>();
                foreach (var p in Open(m.map).Where(p => p.Heard >= m.startedTick))
                {
                    int ayes = p.request?.ayes ?? p.wish.ayes, present = p.request?.present ?? p.wish.present;
                    string what = p.request != null ? p.request.def.label : p.wish.LabelInline;
                    string key = LetterKey(p.request == null ? 0 : p.request.repeal ? 2 : 1, !m.Present(p.Asker), SeatMath.Backed(ayes, present));
                    lines.Add(key.Translate((p.Asker?.LabelShort ?? "?").Named("PAWN"), what.Named("WHAT"), ayes.Named("AYES"), present.Named("PRESENT")).Resolve());
                }
                return lines.Count == 0 ? null : "CC_Business_LetterHead".Translate().Resolve() + " " + string.Join(" ", lines);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("meeting letter: any other business", e);
                return null;
            }
        }
    }

    public partial class CouncilGameComponent
    {
        /// <summary>
        /// Someone would want the same as this petition (Ambitions/MeetingBusiness.cs): the same want is open to them now,
        /// about the same subject and thing, and they haven't asked for it lately.
        /// </summary>
        public bool WantsSame(Wish w, Pawn p)
        {
            if (w?.def == null || p == null || w.Involves(p) || OnCooldown(p, w.def)) return false;
            try
            {
                return SafeCandidates(w.def, p).Any(c => c != null && c.weight > 0f && (c.subjectKey ?? "") == (w.subjectKey ?? "") && c.thingDef == w.thingDef);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("meeting: wants the same", e);
                return false;
            }
        }

        /// <summary>
        /// Someone at the meeting who wants the same joins the petition as they would have anyway: a want the colony backs
        /// together, co-signed; a want of their own, as their own want joining it (when they have no other open).
        /// </summary>
        public void JoinAtMeeting(Wish w, Pawn p)
        {
            if (w?.def == null || p == null || !Settings.petitions || !WantsSame(w, p)) return;
            try
            {
                if (w.def.allowPetition)
                {
                    CoSign(w, p);
                    return;
                }
                if (OpenWishBy(p) != null) return;
                var c = SafeCandidates(w.def, p).FirstOrDefault(x => x != null && x.weight > 0f && (x.subjectKey ?? "") == (w.subjectKey ?? "") && x.thingDef == w.thingDef);
                if (c != null) CreateWish(w.def, p, p, c);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("meeting: joining a petition", e);
            }
        }
    }
}
