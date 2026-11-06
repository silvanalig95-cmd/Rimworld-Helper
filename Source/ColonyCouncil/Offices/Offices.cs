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
        /// <summary>What a candidate for the seat runs on in an election, by what the colony is going through (Offices/ElectionPlanks.cs).</summary>
        public List<OfficePlank> planks;

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

    public partial class OfficeTracker : IExposable
    {
        /// <summary>The colony these seats are of (ColonyState.cs); set by it, not saved.</summary>
        internal ColonyState colony;

        private Dictionary<string, Pawn> holders = new Dictionary<string, Pawn>();
        /// <summary>Holders the colony voted for (to know when the council overrules them).</summary>
        private Dictionary<string, Pawn> elected = new Dictionary<string, Pawn>();
        public int lastElectionYear = -1;
        public int lastElectionTick = -999999;
        /// <summary>An election under way on the map, from its letter to the count (Offices/OfficesElection.cs), or null.</summary>
        public Election election;

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

        /// <summary>Who votes: those of this colony at home with a say (each colony elects its own council).</summary>
        private List<Pawn> Eligible() => PawnsFinder.AllMaps_FreeColonistsSpawned
            .Where(p => !p.IsQuestLodger() && !p.DevelopmentalStage.Juvenile() && !p.IsSlave && CouncilGameComponent.CanVoiceWishes(p) && Ours(p)).ToList();

        /// <summary>Someone of this council's colony (ColonyState.Has).</summary>
        private bool Ours(Pawn p) => colony == null || colony.Has(p);

        /// <summary>
        /// An officer who has gone to live at another of the colonies (they're there now and have been a day): they step
        /// down here. Away on a caravan, or on a short visit, they keep their seat.
        /// </summary>
        private bool MovedAway(Pawn p)
        {
            var home = colony?.Map;
            if (home == null || p == null || colony.Has(p)) return false;
            return ColonyStateMath.MovedAway(Homes.KeyOf(home), Homes.KeyOf(Homes.Of(p)), Homes.KeyOf(Homes.LastHome(p)));
        }

        /// <summary>This colony's home, for its election's dates and texts.</summary>
        private Map HomeMap => colony?.Map;

        /// <summary>The council of a colony (the seat's for a map that isn't a home).</summary>
        public static OfficeTracker For(Map map) => ColonyState.For(map)?.offices;

        /// <summary>
        /// The office someone holds, in whichever colony's council: their own colony's first, then the others' (an officer
        /// away on a visit still holds their seat at home).
        /// </summary>
        public static OfficeDef HeldBy(Pawn p)
        {
            if (p == null) return null;
            var own = ColonyState.Of(p);
            var o = own?.offices.OfficeOf(p);
            if (o != null) return o;
            var all = CouncilGameComponent.Instance?.colonies;
            if (all == null || all.Count <= 1) return null;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == own || c.lostAt != ColonyStateMath.NotLost) continue;
                o = c.offices.OfficeOf(p);
                if (o != null) return o;
            }
            return null;
        }

        /// <summary>The council whose election is held at this map, or null.</summary>
        public static OfficeTracker WithElectionOn(Map map)
        {
            var all = CouncilGameComponent.Instance?.colonies;
            if (all == null || map == null) return null;
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i]?.offices;
                if (t?.election != null && t.election.map == map) return t;
            }
            return null;
        }

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
            foreach (var k in holders.Where(kv => !Valid(kv.Value) || !(DefDatabase<OfficeDef>.GetNamedSilentFail(DefNameOf(kv.Key))?.CanHold(kv.Value) ?? true) || MovedAway(kv.Value))
                         .Select(kv => kv.Key).ToList())
            {
                var gone = holders[k];
                holders.Remove(k);
                elected.Remove(k);
                PersonaMarks.Notify_SeatEmptied(gone, DefNameOf(k));
                if (S.offices && gone != null)
                {
                    var def = DefDatabase<OfficeDef>.GetNamedSilentFail(DefNameOf(k));
                    if (def != null) Tell.Minor(NewsKind.Colony, null, HomeMap, "CC_Office_Vacant".Translate(def.label, gone.LabelShort).Resolve(), null, MessageTypeDefOf.NeutralEvent);
                }
            }
            SyncHediffs();
            if (!S.offices)
            {
                election = null;
                return;
            }
            // The colony's own home and its own year.
            var map = HomeMap;
            if (map == null || Find.TickManager.TicksGame < GenDate.TicksPerDay * 8) return;
            // An election already under way: the day's look is its own (Offices/OfficesElection.cs).
            if (election != null) ElectionDaily(comp);
            else
            {
                if (Eligible().Count < 3) return;
                int year = GenLocalDate.Year(map);
                bool firstEver = lastElectionYear < 0;
                bool newYear = year > lastElectionYear;
                // An open seat with nobody in it, and someone free to fill it (a small colony doesn't vote every few days for seats it can't fill).
                var eligible = Eligible();
                bool vacancy = Offices.Any(o => o.OpenFor(eligible.Count) && Enumerable.Range(0, o.Seats).Any(s => HolderOf(o, s) == null)
                                                && eligible.Any(p => OfficeOf(p) == null && o.CanHold(p)))
                               && Find.TickManager.TicksGame - lastElectionTick > GenDate.TicksPerDay * 3;
                if (firstEver || newYear) Call(comp, year, false);
                else if (vacancy) Call(comp, year, true);
            }
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
            // Rivalries in the peacemaker's own colony only.
            var rivalries = comp.social.ties.Where(t => t.kind == TieKind.Rivals && t.announced && t.a != null && t.b != null
                                                        && t.a.IsColonist && t.b.IsColonist && !t.Involves(peacemaker) && Ours(t.a) && Ours(t.b)).ToList();
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

        /// <summary>
        /// An election is due: with "Elections play out on the map" on, it is announced and held over the next days
        /// (Offices/OfficesElection.cs); otherwise, or where a ruler names the officers, everyone votes at once.
        /// </summary>
        public void Call(CouncilGameComponent comp, int year, bool byElection)
        {
            if (S.electionEvent && Announce(comp, year, byElection)) return;
            Hold(comp, year, byElection);
        }

        /// <summary>
        /// The seats and people of one round of voting (the instant vote or an election's count): who is out of the
        /// running (holders of seats not being voted on, and each seat's winner for the seats after it), and what is
        /// only known once every seat is filled.
        /// </summary>
        private sealed class SeatingRound
        {
            public List<Pawn> voters;
            public Pawn ruler;
            public bool byElection;
            public List<OfficeDef> open;
            public readonly HashSet<Pawn> taken = new HashSet<Pawn>();
            public readonly HashSet<Pawn> winners = new HashSet<Pawn>();
            public readonly HashSet<string> filled = new HashSet<string>();
            /// <summary>The office each winner was voted into, for the seats they leave (Finish).</summary>
            public readonly Dictionary<Pawn, OfficeDef> won = new Dictionary<Pawn, OfficeDef>();
            // Someone voted out of one seat may win another seat later in the same count (the next seat on the elders'
            // bench, or a different office): who lost their place, and who lost the vote, is only known once every seat
            // is filled. <stood>: they stood for that seat again, so they wanted to keep it.
            public readonly List<(OfficeDef office, Pawn who, Pawn to, bool stood)> lost = new List<(OfficeDef, Pawn, Pawn, bool)>();
            public readonly List<(OfficeDef office, Pawn who, Pawn to)> beaten = new List<(OfficeDef, Pawn, Pawn)>();
            /// <summary>Who stood for which seat: voted for themselves in the instant vote, or ran in an election on the map.</summary>
            public readonly HashSet<(OfficeDef, int, Pawn)> stood = new HashSet<(OfficeDef, int, Pawn)>();
            /// <summary>How many seats each has stood for in the instant vote (two at most, as on the map).</summary>
            public readonly Dictionary<Pawn, int> stands = new Dictionary<Pawn, int>();
            /// <summary>Candidates who campaigned (visited voters or spoke at the hustings): they wanted it more.</summary>
            public readonly HashSet<Pawn> campaigned = new HashSet<Pawn>();
            /// <summary>Each seat filled and who took it, in the order counted: the chronicle's one line for the election.</summary>
            public readonly List<(Pawn who, OfficeDef office)> seated = new List<(Pawn, OfficeDef)>();
        }

        /// <summary>The seats few people can fill go first, so the colony's only doctor isn't spent on the speaker's chair.</summary>
        private static IEnumerable<(OfficeDef office, int seat)> SeatOrder(List<OfficeDef> open, List<Pawn> voters)
        {
            foreach (var office in open.OrderBy(o => voters.Count(p => o.CanHold(p))).ThenBy(o => o.order).ToList())
                for (int seat = 0; seat < office.Seats; seat++)
                    yield return (office, seat);
        }

        /// <summary>Only those who can do the work stand, and only those competent enough unless nobody who can is.</summary>
        private static List<Pawn> Standing(OfficeDef office, IEnumerable<Pawn> people)
        {
            var able = people.Where(office.CanHold).ToList();
            if (able.Count == 0) return able;
            float bestAble = able.Max(p => office.Competence(p));
            return able.Where(p => office.Competence(p) >= Mathf.Min(OfficeDef.MinCompetence, bestAble)).ToList();
        }

        /// <summary>Everyone votes, office by office, at once and with one letter; nobody holds two.</summary>
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
            var round = new SeatingRound { voters = voters, ruler = ruler, byElection = byElection, open = Offices.Where(o => o.OpenFor(voters.Count)).ToList() };
            // A seat a colony this size doesn't need yet stays as it is.
            foreach (var office in Offices.Where(o => !o.OpenFor(voters.Count)))
                foreach (var h in HoldersOf(office)) round.taken.Add(h);
            var text = new StringBuilder();
            // A by-election with more than one seat to fill says seats.
            string many = byElection && round.open.Sum(o => Enumerable.Range(0, o.Seats).Count(s => HolderOf(o, s) == null)) > 1 ? "Many" : "";
            text.Append(ruler != null ? (byElection ? "CC_PM_Appoint_ByText" + many : "CC_PM_Appoint_Text").Translate(ruler.Named("PAWN")).Resolve()
                : (byElection ? "CC_Election_ByText" + many : "CC_Election_Text").Translate(year).Resolve());
            if (byElection)
                foreach (var office in round.open)
                    foreach (var h in HoldersOf(office)) round.taken.Add(h);
            foreach (var (office, seat) in SeatOrder(round.open, voters).ToList())
            {
                if (byElection && HolderOf(office, seat) != null) continue;
                var candidates = Standing(office, voters.Where(p => !round.taken.Contains(p)));
                if (candidates.Count == 0) continue;
                var tally = new Dictionary<Pawn, int>();
                foreach (var voter in ballots)
                {
                    var pick = candidates.OrderByDescending(c => Score(office, voter, c)).First();
                    tally[pick] = (tally.TryGetValue(pick, out int n) ? n : 0) + 1;
                    // A vote for oneself is standing for the seat: the first two seats they do it for, as on the map.
                    if (pick == voter && !round.stood.Contains((office, seat, voter)) && (round.stands.TryGetValue(voter, out int had) ? had : 0) < 2)
                    {
                        round.stood.Add((office, seat, voter));
                        round.stands[voter] = had + 1;
                    }
                }
                var ranked = tally.OrderByDescending(kv => kv.Value).ThenByDescending(kv => office.Competence(kv.Key)).ToList();
                string line = ruler != null ? "CC_PM_Appoint_Line".Translate(office.LabelCap, ranked[0].Key.Named("PAWN")).Resolve()
                    : "CC_Election_Line".Translate(office.LabelCap, ranked[0].Key.Named("PAWN"), ranked[0].Value, voters.Count).Resolve();
                Settle(comp, round, office, seat, ranked, line, text);
            }
            string title = ruler != null ? (byElection ? "CC_PM_Appoint_ByTitle" : "CC_PM_Appoint_Title").Translate(year).Resolve()
                : (byElection ? "CC_Election_ByTitle" : "CC_Election_Title").Translate(year).Resolve();
            Finish(comp, round, title, text.ToString(), LookTargets.Invalid);
        }

        /// <summary>
        /// One seat's outcome, from the instant vote or an election's count: the winner takes the seat (and is out of the
        /// running for the seats after it), the letter gets its line, and everything that hangs off a win and a loss
        /// follows (the mood, the journal, the persona's mark, the deed, a loser's grudge, setbacks, the names that count
        /// terms, the chronicle). <paramref name="ranked"/>: the candidates by votes, the winner first.
        /// </summary>
        private void Settle(CouncilGameComponent comp, SeatingRound round, OfficeDef office, int seat, List<KeyValuePair<Pawn, int>> ranked, string line, StringBuilder text)
        {
            // apart: an election's outcome, told in its letter.
            var ruler = round.ruler;
            var voters = round.voters;
            string key = Key(office, seat);
            var winner = ranked[0].Key;
            var previous = HolderOf(office, seat);
            holders[key] = winner;
            elected[key] = winner;
            round.taken.Add(winner);
            round.winners.Add(winner);
            round.won[winner] = office;
            round.filled.Add(key);
            text.Append("\n\n").Append(line);
            var losers = ranked.Skip(1).Where(kv => kv.Value > 0).Select(kv => kv.Key.LabelShort + " (" + kv.Value + ")").ToList();
            if (losers.Count > 0) text.Append(" ").Append("CC_Election_Others".Translate(string.Join(", ", losers)).Resolve());
            if (!office.elected.NullOrEmpty()) text.Append(" ").Append(Flavor.PickText(office.elected).Formatted(winner.Named("PAWN")).Resolve());
            // A speaker the colony has never once voted against (a name: Epithets/NameRecords.cs).
            if (ruler == null && S.epithets && office.defName == "Council_Office_Speaker" && previous == winner && comp.epithets.HolderOf("Council_Epithet_Unbeaten", colony?.Map ?? Homes.Seat) == winner)
                text.Append(" ").Append("CC_Election_Unbeaten".Translate(winner.Named("PAWN")).Resolve());
            CouncilGameComponent.GiveThought(winner, CouncilDefOf.Council_ElectedToOffice);
            comp.journals.Write(winner, "Council_Journal_Elected", null, office.label.Named("OFFICE"));
            PersonaMarks.Notify_Elected(winner, office.label, previous == winner);
            Deeds.Elected(winner, office, voters);
            // Told once every seat is filled (Finish): voted out of this seat may still mean a seat elsewhere.
            if (previous != null && previous != winner && Valid(previous))
                round.lost.Add((office, previous, winner, ranked.Any(kv => kv.Key == previous && kv.Value > 0)));
            // Who wanted it and saw someone else take it: everyone who stood (with "A seat lost is felt"), and the greedy,
            // the jealous and the abrasive who got any votes at all.
            foreach (var kv in ranked.Skip(1))
            {
                if (kv.Key == previous) continue;
                bool stood = S.seatFeelings && round.stood.Contains((office, seat, kv.Key));
                if (stood || (kv.Value > 0 && Vain(kv.Key))) round.beaten.Add((office, kv.Key, winner));
            }
            Setbacks.Notify_OfficeWon(winner, office, ranked.Where(kv => kv.Value > 0).Select(kv => kv.Key));
            // Terms won and votes lost, for the names that count them (Epithets/NameRecords.cs); a bench's seats are shared, so single seats only.
            if (office.Seats == 1)
                NameRecords.Notify_Election(office, winner, ranked.Skip(1).Where(kv => kv.Value > 0).Select(kv => kv.Key)
                    .Concat(previous != null && previous != winner && Valid(previous) ? new[] { previous } : new Pawn[0]), ruler != null);
            // The chronicle has the whole election in one line, once every seat is filled (Finish).
            round.seated.Add((winner, office));
        }

        /// <summary>
        /// The election (or the ruler's naming) as one line of the chronicle: every seat filled and who took it ("Harper as
        /// speaker and Hector as marshal"), everyone seated part of it in the history, so each one's story still has it.
        /// </summary>
        private void ChronicleSeats(CouncilGameComponent comp, SeatingRound round)
        {
            if (round.seated.Count == 0) return;
            if (round.seated.Count == 1)
            {
                var (w, o) = round.seated[0];
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Council, round.ruler != null
                    ? "CC_PM_Chronicle_Named".Translate(round.ruler.LabelShort, w.LabelShort, o.label).Resolve()
                    : "CC_Chronicle_Elected".Translate(w.LabelShort, o.label).Resolve(), w));
                return;
            }
            var seats = round.seated.Select(s => "CC_Chronicle_SeatAs".Translate(s.who.LabelShort.Named("PAWN"), s.office.label.Named("OFFICE")).Resolve()).ToList();
            string line = round.ruler != null
                ? "CC_PM_Chronicle_NamedAll".Translate(round.ruler.LabelShort.Named("RULER"), TextUtil.JoinAnd(seats).Named("SEATS")).Resolve()
                : (round.byElection ? "CC_Chronicle_ElectedBy" : "CC_Chronicle_ElectedAll").Translate(TextUtil.JoinAnd(seats).Named("SEATS")).Resolve();
            var actors = round.seated.Select(s => new HistoryActor(Refs.Of(s.who), HistoryRole.About)).ToList();
            using (History.About("council", Refs.Colony(HomeMap ?? Homes.Seat), actors))
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Council, line, round.seated[0].who));
        }

        /// <summary>Every seat is settled: the loose ends (seats left to their holders, the bench's losers), the edges, and the letter.</summary>
        private void Finish(CouncilGameComponent comp, SeatingRound round, string title, string text, LookTargets look)
        {
            // A seat nobody could be voted into this time (more seats than people, or none left who can do its work)
            // keeps its holder, unless they were just voted into another one: nobody holds two. The letter says so
            // for every such seat, so each open seat is accounted for (ElectionMath.UnvotedSeat).
            var seatLines = new List<string>();
            var empty = new List<(OfficeDef office, int n)>();
            foreach (var office in round.open)
            {
                int emptyHere = 0;
                for (int seat = 0; seat < office.Seats; seat++)
                {
                    string key = Key(office, seat);
                    if (round.filled.Contains(key)) continue;
                    var holder = HolderOf(office, seat);
                    bool left = holder != null && round.winners.Contains(holder);
                    switch (ElectionMath.UnvotedSeat(round.byElection, holder != null, left))
                    {
                        case ElectionMath.Unvoted.Kept when round.ruler == null:
                            seatLines.Add("CC_Elect_SeatKept".Translate(SeatLabel(office, seat), holder.Named("PAWN")).Resolve());
                            break;
                        case ElectionMath.Unvoted.Moved:
                            var to = round.won.TryGetValue(holder, out var o) ? o : null;
                            if (to != null) seatLines.Add("CC_Elect_SeatMoved".Translate(SeatLabel(office, seat), holder.Named("PAWN"), to.label.Named("OFFICE")).Resolve());
                            break;
                        case ElectionMath.Unvoted.Empty:
                            emptyHere++;
                            break;
                    }
                    if (!left) continue;
                    holders.Remove(key);
                    elected.Remove(key);
                }
                if (emptyHere > 0) empty.Add((office, emptyHere));
            }
            if (round.ruler == null && empty.Count > 0)
            {
                var names = empty.Select(x => x.office.Seats > 1
                    ? "CC_Elect_BenchSeats".Translate(Number(x.n).Named("COUNT"), Find.ActiveLanguageWorker.Pluralize(x.office.label).Named("OFFICES")).Resolve()
                    : x.office.label).ToList();
                bool one = empty.Count == 1 && empty[0].n == 1;
                seatLines.Add((one ? "CC_Elect_SeatEmpty" : "CC_Elect_SeatsEmpty").Translate(TextUtil.JoinAnd(names).Named("SEATS")).Resolve());
            }
            if (seatLines.Count > 0) text += "\n\n" + string.Join("\n\n", seatLines);
            // Who lost a seat and who lost a vote, now that every seat is filled (each once). Someone voted onto
            // another seat hasn't lost their place on the council: nothing, or a small "moved" if they stood to keep
            // the old one; someone who minds losing a vote but won a different seat only wanted that one a little more.
            var told = new HashSet<(OfficeDef, Pawn)>();
            var moved = new HashSet<Pawn>();
            foreach (var (office, who, to, stood) in round.lost)
            {
                if (HoldersOf(office).Contains(who) || !told.Add((office, who))) continue;
                var now = OfficeOf(who);
                if (now == null) LostOffice(comp, office, who, to, round.campaigned.Contains(who));
                else if (moved.Add(who)) MovedSeat(comp, office, now, who, stood);
            }
            told.Clear();
            foreach (var (office, who, to) in round.beaten)
            {
                if (HoldersOf(office).Contains(who) || !told.Add((office, who)) || moved.Contains(who)) continue;
                var now = OfficeOf(who);
                if (now == null) LostElection(comp, office, who, to, round.campaigned.Contains(who));
                else if (moved.Add(who)) OtherSeat(comp, office, now, who);
            }
            SyncHediffs();
            ChronicleSeats(comp, round);
            Tell.Letter(NewsKind.Colony, Importance.Notable, HomeMap, title, text, LetterDefOf.NeutralEvent, look);
        }

        /// <summary>
        /// Voted out of their seat (to <paramref name="to"/>): they think less of the winner, the more the more they wanted
        /// it, and (with "A seat lost is felt") a grudge toward them that grows each time they lose to the same person.
        /// </summary>
        private static void LostOffice(CouncilGameComponent comp, OfficeDef office, Pawn who, Pawn to, bool campaigned)
        {
            // apart: an election's outcome, told in its letter (Hold).
            float want = SeatWant(office, who, true, campaigned);
            bool bitter = S.seatFeelings && BondsMath.SeatStage(want) == BondsMath.SeatBitter;
            CouncilGameComponent.GiveThought(who, CouncilDefOf.Council_LostOffice, to, -1f, null, bitter ? 1 : 0);
            comp.journals.Write(who, "Council_Journal_LostOffice", null, office.label.Named("OFFICE"));
            if (S.seatFeelings) SeatGrudge(comp, office, who, to, want, true);
        }

        /// <summary>
        /// How much <paramref name="who"/> wanted the seat they lost (BondsMath.SeatWant): holding it before, a life goal of
        /// a seat on the council (this one or any), a vain nature, a campaign of their own.
        /// </summary>
        private static float SeatWant(OfficeDef office, Pawn who, bool incumbent, bool campaigned)
        {
            var goal = CouncilGameComponent.Instance?.aspirations?.Active(who)?.def;
            bool aspires = goal != null && goal.workerClass == typeof(AspirationWorker_Office) && (goal.office == null || goal.office == office);
            return BondsMath.SeatWant(incumbent, aspires, Vain(who), campaigned);
        }

        /// <summary>Greedy, jealous or abrasive: minds losing a vote.</summary>
        private static bool Vain(Pawn p)
        {
            var t = p?.story?.traits;
            return t != null && (t.HasTrait(TraitDefOf.Greedy) || t.HasTrait(TraitDefOf.Jealous) || t.HasTrait(TraitDefOf.Abrasive));
        }

        /// <summary>
        /// A seat lost to <paramref name="to"/>: a grudge toward the winner by how much it was wanted, more each time it is
        /// the same winner (counted per pair, Social/Bonds.cs); and an abrasive winner rubs it in when the two next meet.
        /// </summary>
        private static void SeatGrudge(CouncilGameComponent comp, OfficeDef office, Pawn who, Pawn to, float want, bool incumbent)
        {
            // apart: an election's outcome, told in its letter; the winner's gloating, if any, is said face to face (Together).
            var social = comp.social;
            if (social == null || !ColonyCouncilMod.Settings.socialWeb) return;
            var r = social.bonds.Get(who, to, true);
            int losses = r?.AddLoss(who) ?? 1;
            string note = (losses >= 2 ? "CC_TieNote_SeatAgain" : incumbent ? "CC_TieNote_SeatLost" : "CC_TieNote_Election").Translate(office.label).Resolve();
            social.Bump(who, to, TieKind.Rivals, BondsMath.SeatGrudge(want, losses, incumbent), note);
            if (!(to.story?.traits?.HasTrait(TraitDefOf.Abrasive) ?? false)) return;
            Together.When("gloat|" + to.thingIDNumber + "|" + who.thingIDNumber, to, who, () =>
            {
                string line = Interplay.Line("Rimfolk_Pool_SeatGloat", to, null, who.Named("OTHER"), office.label.Named("OFFICE"));
                if (line.NullOrEmpty()) return;
                Speech.Say(to, line, who);
                comp.social?.Bump(who, to, TieKind.Rivals, BondsMath.Gloated, "CC_TieNote_Gloated".Translate(office.label).Resolve());
            }, 72f);
        }

        /// <summary>
        /// How high a seat stands, for a move between seats: the speaker's chair above the founding seats (those every
        /// colony has: marshal, quartermaster, physician, steward), and those above the seats a larger colony adds.
        /// </summary>
        public static int SeatRank(OfficeDef o) =>
            o == null ? 0 : o.defName == "Council_Office_Speaker" ? 2 : o.minColonists <= 0 ? 1 : 0;

        /// <summary>
        /// Voted out of <paramref name="from"/> but onto <paramref name="now"/> in the same count: still on the council.
        /// A step down (a lower seat, <see cref="SeatRank"/>) stings like a demotion, the more the further down; a move to
        /// an equal seat only stings if they stood to keep the old one; a move up is just the new seat's joy. The journal
        /// notes the move either way.
        /// </summary>
        private static void MovedSeat(CouncilGameComponent comp, OfficeDef from, OfficeDef now, Pawn who, bool stood)
        {
            // apart: an election's outcome, told in its letter (Hold).
            var move = ElectionMath.Move(SeatRank(from), SeatRank(now), stood);
            bool down = move == ElectionMath.SeatMove.Down || move == ElectionMath.SeatMove.FarDown;
            string cause = "CC_Office_MovedCause".Translate(from.label.Named("OFFICE"), now.label.Named("NEW")).Resolve();
            if (down) CouncilGameComponent.GiveThought(who, CouncilDefOf.Council_MovedDown, null, -1f, cause, move == ElectionMath.SeatMove.FarDown ? 1 : 0);
            else if (move == ElectionMath.SeatMove.Moved) CouncilGameComponent.GiveThought(who, CouncilDefOf.Council_MovedSeat, null, -1f, cause);
            comp.journals.Write(who, down ? "Council_Journal_MovedDown" : "Council_Journal_MovedSeat", null, from.label.Named("OFFICE"), now.label.Named("NEW"));
        }

        /// <summary>
        /// Stood for <paramref name="wanted"/>, lost it, and won <paramref name="now"/> instead (the greedy, the jealous, the
        /// abrasive): minds it a little, a bit more if the seat they wanted stands higher.
        /// </summary>
        private static void OtherSeat(CouncilGameComponent comp, OfficeDef wanted, OfficeDef now, Pawn who)
        {
            // apart: an election's outcome, told in its letter (Hold).
            CouncilGameComponent.GiveThought(who, CouncilDefOf.Council_OtherSeat, null, -1f,
                "CC_Office_OtherSeatCause".Translate(wanted.label.Named("OFFICE"), now.label.Named("NEW")).Resolve(),
                SeatRank(wanted) > SeatRank(now) ? 1 : 0);
        }

        /// <summary>
        /// Stood and lost to <paramref name="to"/>: with "A seat lost is felt", everyone who stood thinks less of the winner,
        /// as much as they wanted the seat, and holds a small grudge that grows with each loss to the same person; without
        /// it, only the greedy, the jealous and the abrasive mind it.
        /// </summary>
        private static void LostElection(CouncilGameComponent comp, OfficeDef office, Pawn who, Pawn to, bool campaigned)
        {
            // apart: an election's outcome, told in its letter (Hold).
            if (!S.seatFeelings)
            {
                CouncilGameComponent.GiveThought(who, CouncilDefOf.Council_LostElection, to);
                comp.social.Bump(who, to, TieKind.Rivals, 10f, "CC_TieNote_Election".Translate(office.label));
                return;
            }
            float want = SeatWant(office, who, false, campaigned);
            int stage = BondsMath.SeatStage(want);
            // The def's stages: 0 the felt one (as it always was), 1 the mild, 2 the bitter.
            CouncilGameComponent.GiveThought(who, CouncilDefOf.Council_LostElection, to, -1f, null, stage == BondsMath.SeatMild ? 1 : stage == BondsMath.SeatBitter ? 2 : 0);
            SeatGrudge(comp, office, who, to, want, false);
        }

        /// <summary>How much this voter wants this candidate in this office, on the day: their lean and the luck of the day.</summary>
        private static float Score(OfficeDef office, Pawn voter, Pawn candidate) => Leaning(office, voter, candidate) + Rand.Range(0f, ElectionMath.VoteNoise);

        /// <summary>
        /// How much this voter wants this candidate in this office, without the luck of the day (the poll reads this):
        /// skill, liking, friendship and rivalry, a vote for oneself, what the colony's names and taken-back promises say,
        /// and, while an election plays out, what the campaign did (<see cref="Election"/>: visits, the hustings, the
        /// council's endorsement).
        /// </summary>
        internal static float Leaning(OfficeDef office, Pawn voter, Pawn candidate)
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
            return s + Interplay.VoteLean(voter, candidate) + Reputations.VoteLean(voter, candidate) + CampaignLean(office, voter, candidate);
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
            // An election under way doesn't vote on this seat any more, nor put them up for another.
            ElectionSeated(office, seat, p);
            bool overrule = current != null && elected.TryGetValue(key, out var e) && e == current;
            if (overrule)
            {
                CouncilGameComponent.GiveThought(current, CouncilDefOf.Council_LostOffice, p);
                comp.standings.Change(current, -8f, "CC_Log_RemovedFromOffice".Translate(office.label));
                comp.social.Bump(current, p, TieKind.Rivals, 15f, "CC_TieNote_Seat".Translate(office.label));
                comp.journals.Write(current, "Council_Journal_LostOffice", null, office.label.Named("OFFICE"));
                foreach (var voter in Eligible().Where(v => v != current && v != p && v.relations != null && v.relations.OpinionOf(current) > v.relations.OpinionOf(p)))
                    comp.standings.Change(voter, -3f, "CC_Log_VoteOverruled".Translate(office.label));
                // direct: the answer to the player's click (appointing in the council window, or calling a vote off).
                Messages.Message("CC_Office_Overruled".Translate(p.Named("PAWN"), office.label.Named("OFFICE"), current.Named("OTHER")).Resolve(), new LookTargets(p), MessageTypeDefOf.NegativeEvent, false);
            }
            else Messages.Message("CC_Office_Appointed".Translate(p.Named("PAWN"), office.label.Named("OFFICE")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent, false);
            CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_ElectedToOffice);
            comp.journals.Write(p, "Council_Journal_Elected", null, office.label.Named("OFFICE"));
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Council, "CC_Chronicle_Appointed".Translate(p.LabelShort, office.label).Resolve(), p));
            Setbacks.Notify_OfficeWon(p, office, current != null ? new[] { current } : null);
            SyncHediffs();
        }

        /// <summary>
        /// A council already formed when the game starts (a scenario's: Scenarios/ScenarioParts.cs): someone takes an empty
        /// seat quietly, before anyone has voted, and this year's election counts as held (an empty seat still has its
        /// by-election). False if the seat is taken, they hold one already, or they can't do the work.
        /// </summary>
        public bool Seat(OfficeDef office, Pawn p, int seat = 0)
        {
            if (office == null || p == null || !Valid(p) || !office.CanHold(p) || OfficeOf(p) != null) return false;
            seat = Mathf.Clamp(seat, 0, office.Seats - 1);
            if (HolderOf(office, seat) != null) return false;
            holders[Key(office, seat)] = p;
            var map = HomeMap ?? Find.AnyPlayerHomeMap;
            if (map != null)
            {
                lastElectionYear = GenLocalDate.Year(map);
                lastElectionTick = Find.TickManager.TicksGame;
            }
            SyncHediffs();
            return true;
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

        /// <summary>
        /// Office holders get their office's edge; everyone of the colony gets the colony-wide ones while the seat is filled
        /// (each colony's council for its own people).
        /// </summary>
        public void SyncHediffs()
        {
            var colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Where(Ours).ToList();
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
                        // A good marshal or physician steadies the grown and the children, not a baby in a crib.
                        bool on = seated.Count > 0 && !p.IsQuestLodger() && !p.DevelopmentalStage.Baby();
                        if (on && h == null) p.health.AddHediff(HediffMaker.MakeHediff(office.colonyHediff, p));
                        else if (!on && h != null) p.health.RemoveHediff(h);
                    }
                }
            }
        }

        /// <summary>A seat's key whose office no longer exists.</summary>
        private static bool Gone(string key) => DefDatabase<OfficeDef>.GetNamedSilentFail(DefNameOf(key)) == null;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref holders, "holders", LookMode.Value, LookMode.Reference, ref k1, ref v1);
            Scribe_Collections.Look(ref elected, "elected", LookMode.Value, LookMode.Reference, ref k2, ref v2);
            Scribe_Values.Look(ref lastElectionYear, "lastElectionYear", -1);
            Scribe_Values.Look(ref lastElectionTick, "lastElectionTick", -999999);
            Scribe_Deep.Look(ref election, "election");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (election != null && (election.races.Count == 0 || election.map == null)) election = null;
                if (holders == null) holders = new Dictionary<string, Pawn>();
                if (elected == null) elected = new Dictionary<string, Pawn>();
                // A seat the council no longer has (the keeper of secrets, retired 5 November 2026) is let go quietly with its holder.
                foreach (var k in holders.Where(kv => kv.Value == null || Gone(kv.Key)).Select(kv => kv.Key).ToList()) holders.Remove(k);
                foreach (var k in elected.Where(kv => kv.Value == null || Gone(kv.Key)).Select(kv => kv.Key).ToList()) elected.Remove(k);
            }
        }
    }
}
