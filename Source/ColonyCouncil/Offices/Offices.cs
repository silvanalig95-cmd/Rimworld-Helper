using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A seat on the council: Speaker, Marshal, Quartermaster, Physician, Steward. The holder gets a small edge
    /// (<see cref="hediff"/>), the colony sometimes too (<see cref="colonyHediff"/>), and the office does its job
    /// in code (see <see cref="OfficeTracker"/> helpers). Filled by a yearly election; the council can overrule it.
    /// </summary>
    public class OfficeDef : Def
    {
        /// <summary>What makes a good candidate: their level in these skills (averaged), plus a passion bonus.</summary>
        public List<SkillDef> skills = new List<SkillDef>();
        public List<TraitDef> goodTraits;
        public List<TraitDef> badTraits;
        public HediffDef hediff;
        /// <summary>While the office is filled, everyone in the colony has this (a good physician, a steady marshal).</summary>
        public HediffDef colonyHediff;
        [MustTranslate] public List<string> duties = new List<string>();
        public int order;

        public float Competence(Pawn p)
        {
            if (p?.skills == null || skills.Count == 0) return 0f;
            float sum = 0f;
            foreach (var sk in skills)
            {
                var r = p.skills.GetSkill(sk);
                if (r == null || r.TotallyDisabled) continue;
                sum += r.Level + (r.passion == Passion.Major ? 3f : r.passion == Passion.Minor ? 1.5f : 0f);
            }
            float c = sum / skills.Count;
            var traits = p.story?.traits;
            if (traits != null)
            {
                if (goodTraits != null) c += goodTraits.Count(traits.HasTrait) * 2f;
                if (badTraits != null) c -= badTraits.Count(traits.HasTrait) * 3f;
            }
            return c;
        }
    }

    public class OfficeTracker : IExposable
    {
        private Dictionary<string, Pawn> holders = new Dictionary<string, Pawn>();
        /// <summary>Holders the colony voted for (to know when the council overrules them).</summary>
        private Dictionary<string, Pawn> elected = new Dictionary<string, Pawn>();
        public int lastElectionYear = -1;
        public int lastElectionTick = -999999;

        private List<string> k1, k2;
        private List<Pawn> v1, v2;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static IEnumerable<OfficeDef> Offices => DefDatabase<OfficeDef>.AllDefsListForReading.OrderBy(o => o.order);

        public Pawn HolderOf(OfficeDef o) => o != null && holders.TryGetValue(o.defName, out var p) && Valid(p) ? p : null;

        public Pawn HolderOf(string defName) => HolderOf(DefDatabase<OfficeDef>.GetNamedSilentFail(defName));

        public OfficeDef OfficeOf(Pawn p) => Offices.FirstOrDefault(o => HolderOf(o) == p);

        public bool Filled(string defName) => HolderOf(defName) != null;

        private static bool Valid(Pawn p) => p != null && !p.Dead && p.IsColonist && !p.IsPrisoner;

        private static List<Pawn> Eligible() => PawnsFinder.AllMaps_FreeColonistsSpawned
            .Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && !p.IsSlave && CouncilGameComponent.CanVoiceWishes(p)).ToList();

        // ---------------- Duties the rest of the mod asks about ----------------

        /// <summary>Speaker chairs council meetings.</summary>
        public Pawn Speaker => S.offices ? HolderOf("Council_Office_Speaker") : null;

        /// <summary>A marshal makes drills count for more.</summary>
        public float DrillFactor => S.offices && Filled("Council_Office_Marshal") ? 1.5f : 1f;

        /// <summary>A quartermaster stretches the stores: dilemmas cost less.</summary>
        public float CostFactor => S.offices && Filled("Council_Office_Quartermaster") ? 0.75f : 1f;

        /// <summary>A steward keeps expectations in check: standards rise slower.</summary>
        public float StandardsFactor => S.offices && Filled("Council_Office_Steward") ? 0.75f : 1f;

        // ---------------- Elections ----------------

        public void Daily(CouncilGameComponent comp)
        {
            foreach (var k in holders.Where(kv => !Valid(kv.Value)).Select(kv => kv.Key).ToList())
            {
                var gone = holders[k];
                holders.Remove(k);
                elected.Remove(k);
                if (S.offices && gone != null)
                {
                    var def = DefDatabase<OfficeDef>.GetNamedSilentFail(k);
                    if (def != null) Messages.Message("CC_Office_Vacant".Translate(def.label, gone.LabelShort).Resolve(), MessageTypeDefOf.NeutralEvent, false);
                }
            }
            SyncHediffs();
            if (!S.offices) return;
            var map = Find.AnyPlayerHomeMap;
            if (map == null || Find.TickManager.TicksGame < GenDate.TicksPerDay * 8) return;
            if (Eligible().Count < 3) return;
            int year = GenLocalDate.Year(map);
            bool firstEver = lastElectionYear < 0;
            bool newYear = year > lastElectionYear;
            bool vacancy = Offices.Any(o => HolderOf(o) == null) && Find.TickManager.TicksGame - lastElectionTick > GenDate.TicksPerDay * 3;
            if (firstEver || newYear) Hold(comp, year, false);
            else if (vacancy) Hold(comp, year, true);
        }

        /// <summary>Everyone votes, office by office; nobody holds two.</summary>
        public void Hold(CouncilGameComponent comp, int year, bool byElection)
        {
            var voters = Eligible();
            if (voters.Count < 2) return;
            lastElectionYear = year;
            lastElectionTick = Find.TickManager.TicksGame;
            var taken = new HashSet<Pawn>();
            var text = new StringBuilder();
            text.Append((byElection ? "CC_Election_ByText" : "CC_Election_Text").Translate(year).Resolve());
            foreach (var office in Offices)
            {
                if (byElection && HolderOf(office) != null)
                {
                    taken.Add(HolderOf(office));
                    continue;
                }
                var candidates = voters.Where(p => !taken.Contains(p)).ToList();
                if (candidates.Count == 0) break;
                var tally = new Dictionary<Pawn, int>();
                foreach (var voter in voters)
                {
                    var pick = candidates.OrderByDescending(c => Score(office, voter, c)).First();
                    tally[pick] = (tally.TryGetValue(pick, out int n) ? n : 0) + 1;
                }
                var ranked = tally.OrderByDescending(kv => kv.Value).ThenByDescending(kv => office.Competence(kv.Key)).ToList();
                var winner = ranked[0].Key;
                var previous = HolderOf(office);
                holders[office.defName] = winner;
                elected[office.defName] = winner;
                taken.Add(winner);
                text.Append("\n\n").Append("CC_Election_Line".Translate(office.LabelCap, winner.Named("PAWN"), ranked[0].Value, voters.Count).Resolve());
                var losers = ranked.Skip(1).Where(kv => kv.Value > 0).Select(kv => kv.Key.LabelShort + " (" + kv.Value + ")").ToList();
                if (losers.Count > 0) text.Append(" ").Append("CC_Election_Others".Translate(string.Join(", ", losers)).Resolve());
                CouncilGameComponent.GiveThought(winner, CouncilDefOf.Council_ElectedToOffice);
                comp.journals.Write(winner, "Council_Journal_Elected", null, office.label.Named("OFFICE"));
                if (previous != null && previous != winner && Valid(previous))
                {
                    CouncilGameComponent.GiveThought(previous, CouncilDefOf.Council_LostOffice, winner);
                    comp.journals.Write(previous, "Council_Journal_LostOffice", null, office.label.Named("OFFICE"));
                }
                foreach (var kv in ranked.Skip(1))
                {
                    var t = kv.Key.story?.traits;
                    if (kv.Value > 0 && t != null && (t.HasTrait(TraitDefOf.Greedy) || t.HasTrait(TraitDefOf.Jealous) || t.HasTrait(TraitDefOf.Abrasive)))
                    {
                        CouncilGameComponent.GiveThought(kv.Key, CouncilDefOf.Council_LostElection, winner);
                        comp.social.Bump(kv.Key, winner, TieKind.Rivals, 10f, "CC_TieNote_Election".Translate(office.label));
                    }
                }
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Council, "CC_Chronicle_Elected".Translate(winner.LabelShort, office.label).Resolve(), winner));
            }
            SyncHediffs();
            Find.LetterStack.ReceiveLetter((byElection ? "CC_Election_ByTitle" : "CC_Election_Title").Translate(year).Resolve(), text.ToString(), LetterDefOf.NeutralEvent);
        }

        /// <summary>How much this voter wants this candidate in this office.</summary>
        private static float Score(OfficeDef office, Pawn voter, Pawn candidate)
        {
            float s = office.Competence(candidate);
            if (voter.relations != null && voter != candidate) s += voter.relations.OpinionOf(candidate) / 12f;
            // Friends, comrades and cliques vote together; rivals vote against.
            if (voter != candidate) s += (CouncilGameComponent.Instance?.social.Warmth(voter, candidate) ?? 0f) / 12f;
            if (voter == candidate)
            {
                var t = voter.story?.traits;
                bool vain = t != null && (t.HasTrait(TraitDefOf.Greedy) || t.HasTrait(TraitDefOf.Abrasive) || t.HasTrait(TraitDefOf.Psychopath));
                s += vain ? 6f : 1.5f;
            }
            return s + Rand.Range(0f, 2.5f);
        }

        /// <summary>The council puts someone else in the seat. Overruling the vote costs the trust of those who wanted the winner.</summary>
        public void Appoint(OfficeDef office, Pawn p, CouncilGameComponent comp)
        {
            if (office == null || p == null) return;
            var current = HolderOf(office);
            if (current == p) return;
            foreach (var other in Offices.Where(o => HolderOf(o) == p).ToList()) holders.Remove(other.defName);
            holders[office.defName] = p;
            bool overrule = current != null && elected.TryGetValue(office.defName, out var e) && e == current;
            if (overrule)
            {
                CouncilGameComponent.GiveThought(current, CouncilDefOf.Council_LostOffice, p);
                comp.standings.Change(current, -8f, "CC_Log_RemovedFromOffice".Translate(office.label));
                comp.social.Bump(current, p, TieKind.Rivals, 15f, "CC_TieNote_Seat".Translate(office.label));
                comp.journals.Write(current, "Council_Journal_LostOffice", null, office.label.Named("OFFICE"));
                foreach (var voter in Eligible().Where(v => v != current && v != p && v.relations != null && v.relations.OpinionOf(current) > v.relations.OpinionOf(p)))
                    comp.standings.Change(voter, -3f, "CC_Log_VoteOverruled".Translate(office.label));
                Messages.Message("CC_Office_Overruled".Translate(p.Named("PAWN"), office.label.Named("OFFICE"), current.Named("OTHER")).Resolve(), new LookTargets(p), MessageTypeDefOf.NegativeEvent, false);
            }
            else Messages.Message("CC_Office_Appointed".Translate(p.Named("PAWN"), office.label.Named("OFFICE")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent, false);
            CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_ElectedToOffice);
            comp.journals.Write(p, "Council_Journal_Elected", null, office.label.Named("OFFICE"));
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Council, "CC_Chronicle_Appointed".Translate(p.LabelShort, office.label).Resolve(), p));
            SyncHediffs();
        }

        public void Dismiss(OfficeDef office, CouncilGameComponent comp)
        {
            var current = HolderOf(office);
            if (current == null) return;
            holders.Remove(office.defName);
            CouncilGameComponent.GiveThought(current, CouncilDefOf.Council_LostOffice, null);
            comp.standings.Change(current, -6f, "CC_Log_RemovedFromOffice".Translate(office.label));
            comp.journals.Write(current, "Council_Journal_LostOffice", null, office.label.Named("OFFICE"));
            SyncHediffs();
        }

        /// <summary>Office holders get their office's edge; everyone gets the colony-wide ones while the seat is filled.</summary>
        public void SyncHediffs()
        {
            var colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists;
            foreach (var office in Offices)
            {
                var holder = S.offices ? HolderOf(office) : null;
                if (office.hediff != null)
                {
                    foreach (var p in colonists)
                    {
                        var h = p.health?.hediffSet?.GetFirstHediffOfDef(office.hediff);
                        if (p == holder && h == null) p.health.AddHediff(HediffMaker.MakeHediff(office.hediff, p));
                        else if (p != holder && h != null) p.health.RemoveHediff(h);
                    }
                }
                if (office.colonyHediff != null)
                {
                    foreach (var p in colonists)
                    {
                        var h = p.health?.hediffSet?.GetFirstHediffOfDef(office.colonyHediff);
                        bool on = holder != null && !p.IsQuestLodger();
                        if (on && h == null) p.health.AddHediff(HediffMaker.MakeHediff(office.colonyHediff, p));
                        else if (!on && h != null) p.health.RemoveHediff(h);
                    }
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref holders, "holders", LookMode.Value, LookMode.Reference, ref k1, ref v1);
            Scribe_Collections.Look(ref elected, "elected", LookMode.Value, LookMode.Reference, ref k2, ref v2);
            Scribe_Values.Look(ref lastElectionYear, "lastElectionYear", -1);
            Scribe_Values.Look(ref lastElectionTick, "lastElectionTick", -999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (holders == null) holders = new Dictionary<string, Pawn>();
                if (elected == null) elected = new Dictionary<string, Pawn>();
                foreach (var k in holders.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) holders.Remove(k);
                foreach (var k in elected.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList()) elected.Remove(k);
            }
        }
    }
}
