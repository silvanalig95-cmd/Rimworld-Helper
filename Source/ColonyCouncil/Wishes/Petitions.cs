using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Shared wants as one petition (phase 6 of the review, part 1: the player's decision 11 of 6 November 2026). When a
    /// colonist voices a want of their own (proper armor, a roomier bedroom) that someone else in the colony already waits
    /// for, it joins theirs: the council hears one petition with all their names, one deadline and one answer. Each want
    /// stays its own wish underneath (each fighter's armor is their own), so each is answered on the map as it is met, with
    /// its own thanks as a word; the letter waits for the last and names everyone. A promise, a refusal or setting it aside
    /// answers them all at once, and a petition let lapse is told once. A wish many back for one thing (a rec room) was
    /// always one wish with co-signers and stays so. The rules are <see cref="PetitionMath"/>'s.
    /// </summary>
    public partial class CouncilGameComponent
    {
        // ================= Who is in it =================

        /// <summary>The open wants of this wish's petition, the oldest first; just the wish itself when it stands alone.</summary>
        public List<Wish> PetitionOf(Wish w)
        {
            var list = new List<Wish>();
            if (w == null) return list;
            if (w.petition < 0)
            {
                if (w.state == WishState.Open) list.Add(w);
                return list;
            }
            for (int i = 0; i < wishes.Count; i++)
            {
                var o = wishes[i];
                if (o.state == WishState.Open && o.petition == w.petition) list.Add(o);
            }
            if (w.state == WishState.Open && !list.Contains(w)) list.Add(w);
            list.Sort((a, b) => a.createdTick != b.createdTick ? a.createdTick.CompareTo(b.createdTick) : a.id.CompareTo(b.id));
            return list;
        }

        /// <summary>More than one name on it.</summary>
        public bool IsPetition(Wish w) => w != null && w.petition >= 0 && PetitionOf(w).Count > 1;

        /// <summary>
        /// The want that stands for its petition before the council (on the wishes page, in the inbox, for the reminders and
        /// the last call): a wish on its own does; in a petition, its oldest open want.
        /// </summary>
        public bool LeadsPetition(Wish w)
        {
            if (w == null) return false;
            if (w.petition < 0) return true;
            var all = PetitionOf(w);
            return all.Count == 0 || all[0] == w;
        }

        /// <summary>"Stace, Dukialgac and Harper": who wants it, the one who asked first first.</summary>
        public string PetitionNames(Wish w) => TextUtil.JoinAnd(PetitionOf(w).Select(o => o.pawn?.LabelShort ?? "?").ToList());

        /// <summary>Everyone who asked, for links and portraits.</summary>
        public List<Pawn> PetitionPeople(Wish w) => PetitionOf(w).Select(o => o.pawn).Where(p => p != null).ToList();

        // ================= Joining =================

        /// <summary>
        /// The open want a new one of the same thing joins (<see cref="PetitionMath.Join"/>): someone else's in the same
        /// colony, a want of their own both, about the same subject and thing. Null: a wish of its own.
        /// </summary>
        private Wish ShareWith(Wish w)
        {
            if (w?.def == null || w.pawn == null) return null;
            try
            {
                var open = wishes.Where(o => o != w && o.state == WishState.Open && o.def == w.def && o.pawn != null).ToList();
                if (open.Count == 0) return null;
                var colonies = new List<ColonyState>();
                int ColonyNo(Pawn p)
                {
                    var c = ColonyState.Of(p);
                    int i = colonies.IndexOf(c);
                    if (i >= 0) return i;
                    colonies.Add(c);
                    return colonies.Count - 1;
                }
                WantKey Key(Wish o) => new WantKey
                {
                    id = o.id, def = o.def.defName, subject = o.subjectKey ?? "", thing = o.thingDef?.defName ?? "", asker = o.pawn.thingIDNumber,
                    colony = ColonyNo(o.pawn), petition = o.petition, created = o.createdTick,
                    shareable = PetitionMath.Shareable(o.def.allowPetition, o.ForSomeoneElse)
                };
                var keys = open.Select(Key).ToList();
                int j = PetitionMath.Join(keys, Key(w));
                return j >= 0 ? open[j] : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("petitions: joining", e);
                return null;
            }
        }

        /// <summary>
        /// A new want joins <paramref name="lead"/>'s: one petition, one deadline for all of it (<see cref="PetitionMath.Deadline"/>),
        /// the promise and the reminders as they stand; said in a word and a line over their head, not a letter of its own.
        /// </summary>
        private void JoinPetition(Wish w, Wish lead)
        {
            int now = Find.TickManager.TicksGame;
            int p = lead.petition >= 0 ? lead.petition : lead.id;
            var all = PetitionOf(lead);
            lead.petition = p;
            w.petition = p;
            int deadline = PetitionMath.Deadline(all.Max(o => o.expiresTick), now, w.expiresTick - now, GenDate.TicksPerDay);
            w.promised = all.Any(o => o.promised);
            w.stage = all.Max(o => o.stage);
            foreach (var o in all) o.expiresTick = deadline;
            w.expiresTick = deadline;
            wishes.Add(w);
            var everyone = PetitionOf(w);
            string names = TextUtil.JoinAnd(everyone.Select(o => o.pawn.LabelShort).ToList());
            // apart: a want joins the petition for the same thing; the line is said aloud, to nobody in particular.
            Tell.Message(NewsKind.Colony, Importance.Notable, null,
                "CC_Petition_Joined".Translate(w.pawn.Named("PAWN"), w.LabelInline.Named("WISH"), names.Named("NAMES"), everyone.Count.Named("COUNT")).Resolve()
                + " \"" + TextUtil.ShortSpeech(w.quote, 120) + "\"",
                new LookTargets(w.pawn), MessageTypeDefOf.NeutralEvent);
            Speak(w.pawn, w.quote, new Color(1f, 0.92f, 0.6f));
            // Those who ask together think a little better of each other for it.
            foreach (var o in everyone)
                if (o != w && o.pawn != null)
                    social.Bump(w.pawn, o.pawn, TieKind.Friends, 2f, "CC_TieNote_Petition".Translate(w.Label));
            AddChronicle(new ChronicleEntry(ChronicleKind.Petition,
                "CC_Chronicle_PetitionJoined".Translate(w.pawn.LabelShort, w.LabelInline, names).Resolve(), w.pawn));
        }

        // ================= Answering all of it =================

        /// <summary>A promise, a refusal or setting it aside for the whole petition: the others first, quietly, then the one who leads it, told.</summary>
        private void ForPetition(Wish w, Action<Wish> each)
        {
            var all = PetitionOf(w);
            foreach (var o in all)
            {
                if (o == w) continue;
                o.toldWithPetition = true;
                each(o);
                o.toldWithPetition = false;
            }
            each(w);
        }

        /// <summary>The names of a petition's want for the message that answers it all: "Stace, Dukialgac and Harper".</summary>
        private string NamesFor(Wish w, List<Wish> all) => all.Count > 1 ? TextUtil.JoinAnd(all.Select(o => o.pawn.LabelShort).ToList()) : null;

        // ================= Met one by one, the letter at the last =================

        /// <summary>
        /// One want of a petition met on the map: the others still waiting hear of it, and remember the name for the letter
        /// that comes when the last is met. True when others still wait (its own thanks come as a word, not a letter).
        /// </summary>
        private bool MetInPetition(Wish w, Pawn who, out List<Pawn> metBefore)
        {
            metBefore = w.petitionMet.Where(p => p != null).ToList();
            if (w.petition < 0) return false;
            var others = PetitionOf(w).Where(o => o != w).ToList();
            foreach (var o in others)
            {
                foreach (var p in metBefore)
                    if (!o.petitionMet.Contains(p)) o.petitionMet.Add(p);
                if (!o.petitionMet.Contains(who)) o.petitionMet.Add(who);
            }
            return !PetitionMath.LetterAtLast(others.Count);
        }

        /// <summary>
        /// The petition lapses: everyone in it whose deadline has come lapses with this one, told once (the others' own
        /// lapses are quiet). Returns the names, or null when it stood alone.
        /// </summary>
        private string LapseTogether(Wish w)
        {
            if (w.petition < 0 || w.toldWithPetition) return null;
            int now = Find.TickManager.TicksGame;
            var with = PetitionOf(w).Where(o => o != w && now >= o.expiresTick).ToList();
            if (with.Count == 0) return null;
            foreach (var o in with) o.toldWithPetition = true;
            return TextUtil.JoinAnd(new[] { w }.Concat(with).OrderBy(o => o.createdTick).Select(o => o.pawn.LabelShort).ToList());
        }
    }
}
