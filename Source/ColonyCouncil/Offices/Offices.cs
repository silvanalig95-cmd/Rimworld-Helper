using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A seat on the council: Speaker, Marshal, Quartermaster, Physician, Steward, and for a bigger colony the Reeve,
    /// Peacemaker, Keeper of Days, Herdmaster, Chaplain (with Ideology), Warden of Guests and the council of elders (three
    /// seats). The holder gets a small edge (<see cref="hediff"/>), the colony sometimes too (<see cref="colonyHediff"/>),
    /// and the office does its job in code (see <see cref="OfficeTracker"/> helpers and Interplay/). Filled by a yearly
    /// election; the council can overrule it.
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
        /// <summary>The seat only opens once the colony has this many people who can vote (small colonies need fewer offices).</summary>
        public int minColonists;
        /// <summary>A line for the election letter after the winner's name, one picked at random ({PAWN_...} the winner).</summary>
        [MustTranslate] public List<string> elected;
        /// <summary>How many hold it at once (the council of elders sits three).</summary>
        public int seats = 1;
        /// <summary>Only with Ideology, for a colony with a faith of its own (the chaplain).</summary>
        public bool needsIdeology;
        /// <summary>Competence for each ten years of age over thirty (up to seventy): seats that want the old.</summary>
        public float ageWeight;
        /// <summary>Competence for being of the colony's own faith, and as much against for another (Ideology).</summary>
        public float faithWeight;

        /// <summary>The seat exists in this game: with Ideology and a faith for the chaplain.</summary>
        public bool Available => !needsIdeology || DecreeWork.ColonyFaith != null;

        public bool OpenFor(int voters) => voters >= minColonists && Available;

        public int Seats => Mathf.Max(1, seats);

        /// <summary>
        /// Whether they could do this office's work at all: not when every skill it needs is one they can't use (a
        /// colonist incapable of medicine is never the physician), nor when every kind of work those skills are for is
        /// closed to them. The vote still decides among those who can.
        /// </summary>
        public bool CanHold(Pawn p)
        {
            if (p?.skills == null) return false;
            if (skills.Count == 0) return true;
            foreach (var sk in skills)
            {
                var r = p.skills.GetSkill(sk);
                if (r == null || r.TotallyDisabled) continue;
                bool anyWork = false, anyOpen = false;
                foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                {
                    if (wt.relevantSkills == null || !wt.relevantSkills.Contains(sk)) continue;
                    anyWork = true;
                    if (!p.WorkTypeIsDisabled(wt))
                    {
                        anyOpen = true;
                        break;
                    }
                }
                if (!anyWork || anyOpen) return true;
            }
            return false;
        }

        /// <summary>The least competence the seat asks for, unless nobody who can hold it has more.</summary>
        public const float MinCompetence = 4f;

        public float Competence(Pawn p)
        {
            if (p?.skills == null || skills.Count == 0) return 0f;
            float sum = 0f;
            foreach (var sk in skills)
            {
                var r = p.skills.GetSkill(sk);
                if (r == null || r.TotallyDisabled) continue;
                sum += r.Level + People.PassionLevel(r) * 1.5f;
            }
            float c = sum / skills.Count;
            var traits = p.story?.traits;
            if (traits != null)
            {
                if (goodTraits != null) c += goodTraits.Count(traits.HasTrait) * 2f;
                if (badTraits != null) c -= badTraits.Count(traits.HasTrait) * 3f;
            }
            if (ageWeight != 0f && p.RaceProps != null && p.RaceProps.Humanlike)
                c += ageWeight * Mathf.Clamp(Flavor.HumanAge(p) - 30f, 0f, 40f) / 10f;
            if (faithWeight != 0f)
            {
                if (DecreeWork.Believer(p)) c += faithWeight;
                else if (DecreeWork.OtherFaith(p)) c -= faithWeight;
            }
            // Other mods' traits and classes, by name (ModPeopleDef): a healer for physician, a fighter for marshal.
            return c + ModPeople.OfficeBonus(this, p);
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

        /// <summary>The key a seat is kept under: the office's defName, and "|2", "|3" for the further seats of a bench.</summary>
        private static string Key(OfficeDef o, int seat) => seat <= 0 ? o.defName : o.defName + "|" + (seat + 1);

        /// <summary>The office a key belongs to (its defName, without the seat).</summary>
        private static string DefNameOf(string key)
        {
            int bar = key?.IndexOf('|') ?? -1;
            return bar < 0 ? key : key.Substring(0, bar);
        }

        public Pawn HolderOf(OfficeDef o) => HolderOf(o, 0);

        /// <summary>Who holds one seat of an office (seat 0 is the first, and the only one of most offices).</summary>
        public Pawn HolderOf(OfficeDef o, int seat) => o != null && holders.TryGetValue(Key(o, seat), out var p) && Valid(p) ? p : null;

        /// <summary>Everyone holding a seat of this office.</summary>
        public List<Pawn> HoldersOf(OfficeDef o)
        {
            var list = new List<Pawn>();
            if (o == null) return list;
            for (int s = 0; s < o.Seats; s++)
            {
                var p = HolderOf(o, s);
                if (p != null && !list.Contains(p)) list.Add(p);
            }
            return list;
        }

        public Pawn HolderOf(string defName) => HolderOf(DefDatabase<OfficeDef>.GetNamedSilentFail(defName));

        public List<Pawn> HoldersOf(string defName) => HoldersOf(DefDatabase<OfficeDef>.GetNamedSilentFail(defName));

        public OfficeDef OfficeOf(Pawn p) => p == null ? null : Offices.FirstOrDefault(o => HoldersOf(o).Contains(p));

        public bool Filled(string defName) => HolderOf(defName) != null;

        private static bool Valid(Pawn p) => People.HasInnerLife(p) && p.IsColonist && !p.IsPrisoner;

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

        /// <summary>A peacemaker sits feuding colonists down now and then, and rivalries cool a little every day.</summary>
        public Pawn Peacemaker => S.offices ? HolderOf("Council_Office_Peacemaker") : null;

        /// <summary>A keeper of days makes every kept custom and calendar day a little more of an occasion.</summary>
        public Pawn KeeperOfDays => S.offices ? HolderOf("Council_Office_Keeper") : null;

        // ---------------- Elections ----------------

        public void Daily(CouncilGameComponent comp)
        {
            // A holder who can no longer do the seat's work (an injury, a new incapability) steps down with the dead and
            // the departed.
            foreach (var k in holders.Where(kv => !Valid(kv.Value) || !(DefDatabase<OfficeDef>.GetNamedSilentFail(DefNameOf(kv.Key))?.CanHold(kv.Value) ?? true))
                         .Select(kv => kv.Key).ToList())
            {
                var gone = holders[k];
                holders.Remove(k);
                elected.Remove(k);
                if (S.offices && gone != null)
                {
                    var def = DefDatabase<OfficeDef>.GetNamedSilentFail(DefNameOf(k));
                    if (def != null) News.Post("CC_Office_Vacant".Translate(def.label, gone.LabelShort).Resolve(), null, NewsKind.Colony, MessageTypeDefOf.NeutralEvent);
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
            // An open seat with nobody in it, and someone free to fill it (a small colony doesn't vote every few days for seats it can't fill).
            var eligible = Eligible();
            bool vacancy = Offices.Any(o => o.OpenFor(eligible.Count) && Enumerable.Range(0, o.Seats).Any(s => HolderOf(o, s) == null)
                                            && eligible.Any(p => OfficeOf(p) == null && o.CanHold(p)))
                           && Find.TickManager.TicksGame - lastElectionTick > GenDate.TicksPerDay * 3;
            if (firstEver || newYear) Hold(comp, year, false);
            else if (vacancy) Hold(comp, year, true);
            try
            {
                Peacemaking(comp);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("peacemaker", e);
            }
        }

        // ---------------- What the newer offices do ----------------

        /// <summary>
        /// The peacemaker's day: every rivalry between colonists cools a little, and now and then the peacemaker sits the
        /// worst-feuding pair down together (counted as a quarrel mended): when the three of them are in one place
        /// (a meal, an evening in the rec room), within two days (Together).
        /// </summary>
        private void Peacemaking(CouncilGameComponent comp)
        {
            var peacemaker = Peacemaker;
            if (peacemaker == null || !ColonyCouncilMod.Settings.socialWeb || comp.social == null) return;
            var rivalries = comp.social.ties.Where(t => t.kind == TieKind.Rivals && t.announced && t.a != null && t.b != null
                                                        && t.a.IsColonist && t.b.IsColonist && !t.Involves(peacemaker)).ToList();
            foreach (var t in rivalries) comp.social.Weaken(t.a, t.b, TieKind.Rivals, 0.5f, null);
            if (!peacemaker.Spawned || peacemaker.Downed || !Rand.Chance(0.15f)) return;
            var pair = rivalries.Where(t => comp.social.ties.Contains(t) && t.a.Spawned && t.b.Spawned && t.a.Map == peacemaker.Map && t.b.Map == peacemaker.Map)
                .OrderByDescending(t => t.strength).FirstOrDefault();
            if (pair == null) return;
            var (a, b) = (pair.a, pair.b);
            Together.When("mediate|" + a.thingIDNumber + "|" + b.thingIDNumber, peacemaker, a, () => Mediate(comp, peacemaker, pair), 48f, -1f,
                () => Together.Can(peacemaker, b) && Together.Can(a, b));
        }

        /// <summary>The peacemaker with the feuding pair, all three together: they're sat down and talked round.</summary>
        private void Mediate(CouncilGameComponent comp, Pawn peacemaker, SocialTie pair)
        {
            // together: called once the peacemaker and both of the pair are together (Peacemaking, through Together.When).
            if (Peacemaker != peacemaker || !comp.social.ties.Contains(pair)) return;
            using var _cause = Causes.Push("CC_Cause_Peacemaker".Translate(peacemaker.LabelShort).Resolve());
            comp.social.Weaken(pair.a, pair.b, TieKind.Rivals, 6f, "CC_TieNote_Peacemaker".Translate(peacemaker.LabelShort));
            CouncilRecords.Add(peacemaker, CouncilRecords.QuarrelsMended);
            string line = Flavor.FromPool("Council_Pool_PeacemakerMediates", peacemaker);
            if (line.NullOrEmpty()) return;
            string text = line.Formatted(peacemaker.Named("PAWN"), pair.a.Named("A"), pair.b.Named("B")).Resolve();
            News.Post(text, new LookTargets(peacemaker, pair.a, pair.b), NewsKind.People, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
            SocialLog.Add(peacemaker, pair.a, text);
        }

        /// <summary>
        /// A council tradition or a calendar day was kept: with a keeper of days in office, everyone there remembers it as
        /// a day done properly (a small, short good mood).
        /// </summary>
        public void Notify_CustomKept(IEnumerable<Pawn> colonists)
        {
            var keeper = KeeperOfDays;
            if (keeper == null || colonists == null) return;
            var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Office_WellKept");
            if (thought == null) return;
            string cause = "CC_Cause_KeeperOfDays".Translate(keeper.LabelShort).Resolve();
            foreach (var p in colonists)
                if (p != null && p.IsColonist && !p.Dead) CouncilGameComponent.GiveThought(p, thought, null, -1f, cause);
        }

        /// <summary>Everyone votes, office by office; nobody holds two.</summary>
        public void Hold(CouncilGameComponent comp, int year, bool byElection)
        {
            using var _cause = Causes.Push("CC_Cause_Election".Translate().Resolve());
            // apart: an election, told in its letter.
            var voters = Eligible();
            if (voters.Count < 2) return;
            // Beliefs that put a ruler above the vote (aristocratic, royal): the leader names the officers.
            var ruler = CouncilCustoms.Appointer(voters);
            var ballots = ruler != null ? new List<Pawn> { ruler } : voters;
            lastElectionYear = year;
            lastElectionTick = Find.TickManager.TicksGame;
            var taken = new HashSet<Pawn>();
            // A seat a colony this size doesn't need yet stays as it is.
            foreach (var office in Offices.Where(o => !o.OpenFor(voters.Count)))
                foreach (var h in HoldersOf(office)) taken.Add(h);
            var text = new StringBuilder();
            text.Append(ruler != null ? (byElection ? "CC_PM_Appoint_ByText" : "CC_PM_Appoint_Text").Translate(ruler.Named("PAWN")).Resolve()
                : (byElection ? "CC_Election_ByText" : "CC_Election_Text").Translate(year).Resolve());
            var open = Offices.Where(o => o.OpenFor(voters.Count)).ToList();
            if (byElection)
                foreach (var office in open)
                    foreach (var h in HoldersOf(office)) taken.Add(h);
            var winners = new HashSet<Pawn>();
            var filled = new HashSet<string>();
            // The seats few people can fill go first, so the colony's only doctor isn't spent on the speaker's chair.
            foreach (var office in open.OrderBy(o => voters.Count(p => o.CanHold(p))).ThenBy(o => o.order).ToList())
            for (int seat = 0; seat < office.Seats; seat++)
            {
                string key = Key(office, seat);
                if (byElection && HolderOf(office, seat) != null) continue;
                // Only those who can do the work stand, and only those competent enough unless nobody who can is.
                var able = voters.Where(p => !taken.Contains(p) && office.CanHold(p)).ToList();
                if (able.Count == 0) continue;
                float bestAble = able.Max(p => office.Competence(p));
                var candidates = able.Where(p => office.Competence(p) >= Mathf.Min(OfficeDef.MinCompetence, bestAble)).ToList();
                if (candidates.Count == 0) continue;
                var tally = new Dictionary<Pawn, int>();
                foreach (var voter in ballots)
                {
                    var pick = candidates.OrderByDescending(c => Score(office, voter, c)).First();
                    tally[pick] = (tally.TryGetValue(pick, out int n) ? n : 0) + 1;
                }
                var ranked = tally.OrderByDescending(kv => kv.Value).ThenByDescending(kv => office.Competence(kv.Key)).ToList();
                var winner = ranked[0].Key;
                var previous = HolderOf(office, seat);
                holders[key] = winner;
                elected[key] = winner;
                taken.Add(winner);
                winners.Add(winner);
                filled.Add(key);
                text.Append("\n\n").Append(ruler != null ? "CC_PM_Appoint_Line".Translate(office.LabelCap, winner.Named("PAWN")).Resolve()
                    : "CC_Election_Line".Translate(office.LabelCap, winner.Named("PAWN"), ranked[0].Value, voters.Count).Resolve());
                var losers = ranked.Skip(1).Where(kv => kv.Value > 0).Select(kv => kv.Key.LabelShort + " (" + kv.Value + ")").ToList();
                if (losers.Count > 0) text.Append(" ").Append("CC_Election_Others".Translate(string.Join(", ", losers)).Resolve());
                if (!office.elected.NullOrEmpty()) text.Append(" ").Append(Flavor.PickText(office.elected).Formatted(winner.Named("PAWN")).Resolve());
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
                Setbacks.Notify_OfficeWon(winner, office, ranked.Where(kv => kv.Value > 0).Select(kv => kv.Key));
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Council, ruler != null
                    ? "CC_PM_Chronicle_Named".Translate(ruler.LabelShort, winner.LabelShort, office.label).Resolve()
                    : "CC_Chronicle_Elected".Translate(winner.LabelShort, office.label).Resolve(), winner));
            }
            // A seat nobody could be voted into this time (more seats than people, or none left who can do its work)
            // keeps its holder, unless they were just voted into another one: nobody holds two.
            foreach (var office in open)
                for (int seat = 0; seat < office.Seats; seat++)
                {
                    string key = Key(office, seat);
                    if (filled.Contains(key)) continue;
                    var holder = HolderOf(office, seat);
                    if (holder == null || !winners.Contains(holder)) continue;
                    holders.Remove(key);
                    elected.Remove(key);
                }
            SyncHediffs();
            string title = ruler != null ? (byElection ? "CC_PM_Appoint_ByTitle" : "CC_PM_Appoint_Title").Translate(year).Resolve()
                : (byElection ? "CC_Election_ByTitle" : "CC_Election_Title").Translate(year).Resolve();
            Find.LetterStack.ReceiveLetter(title, text.ToString(), LetterDefOf.NeutralEvent);
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
            // A promise taken back counts against those in office; the names the colony gave count for them (Interplay/).
            return s + Interplay.VoteLean(voter, candidate) + Rand.Range(0f, 2.5f);
        }

        /// <summary>The council puts someone else in the seat. Overruling the vote costs the trust of those who wanted the winner.</summary>
        public void Appoint(OfficeDef office, Pawn p, CouncilGameComponent comp, int seat = 0)
        {
            if (office == null || p == null || !office.CanHold(p)) return;
            using var _cause = Causes.Push("CC_Cause_Appointed".Translate(p.LabelShort, office.label).Resolve());
            // apart: the council names an officer (a message).
            seat = Mathf.Clamp(seat, 0, office.Seats - 1);
            string key = Key(office, seat);
            var current = HolderOf(office, seat);
            if (current == p) return;
            foreach (var k in holders.Where(kv => kv.Value == p).Select(kv => kv.Key).ToList()) holders.Remove(k);
            holders[key] = p;
            bool overrule = current != null && elected.TryGetValue(key, out var e) && e == current;
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
            Setbacks.Notify_OfficeWon(p, office, current != null ? new[] { current } : null);
            SyncHediffs();
        }

        public void Dismiss(OfficeDef office, CouncilGameComponent comp, int seat = 0)
        {
            if (office == null) return;
            using var _cause = Causes.Push("CC_Cause_Dismissed".Translate(office.label).Resolve());
            var current = HolderOf(office, seat);
            if (current == null) return;
            holders.Remove(Key(office, seat));
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
                var seated = S.offices ? HoldersOf(office) : new List<Pawn>();
                if (office.hediff != null)
                {
                    foreach (var p in colonists)
                    {
                        var h = p.health?.hediffSet?.GetFirstHediffOfDef(office.hediff);
                        bool holds = seated.Contains(p);
                        if (holds && h == null) p.health.AddHediff(HediffMaker.MakeHediff(office.hediff, p));
                        else if (!holds && h != null) p.health.RemoveHediff(h);
                    }
                }
                if (office.colonyHediff != null)
                {
                    foreach (var p in colonists)
                    {
                        var h = p.health?.hediffSet?.GetFirstHediffOfDef(office.colonyHediff);
                        bool on = seated.Count > 0 && !p.IsQuestLodger();
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
