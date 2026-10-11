using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>An enemy who struck down, maimed or killed one of ours and got away: a raider, or a beast.</summary>
    public class Nemesis : IExposable
    {
        public Pawn pawn;
        public Pawn victim;
        public string victimName;
        public string name;
        /// <summary>downed, maimed or killed.</summary>
        public string deed;
        public int tick;
        /// <summary>Got away and is known to the colony.</summary>
        public bool announced;
        public bool onMap = true;
        public int returns;
        public bool captured;
        public bool ended;
        public int endedTick;
        /// <summary>How it ended, for the Story tab.</summary>
        public string endText;
        public int lastSeedThreat;
        /// <summary>Last time they were on one of our maps (or got away); long silence ends it.</summary>
        public int lastSeen;

        public bool IsBeast => pawn?.RaceProps != null && pawn.RaceProps.Animal;

        /// <summary>
        /// A scenario's enemy (they hunt, were robbed by, betrayed or held someone): their quarrel is with the whole
        /// colony, so it outlasts the one it names.
        /// </summary>
        public bool Sworn => deed == "hunts" || deed == "robbed" || deed == "betrayed" || deed == "enslaved";

        public string Name => pawn != null && !pawn.Discarded ? pawn.LabelShort : name ?? "?";

        public string DeedText => ("CC_Nemesis_Deed_" + (deed ?? "downed")).Translate(victimName ?? "?").Resolve();

        /// <summary>"still out there", "here, now", "our prisoner", or how it ended.</summary>
        public string Status
        {
            get
            {
                if (ended)
                {
                    if (pawn != null && pawn.Dead) return "CC_Nemesis_Status_Dead".Translate();
                    if (pawn != null && !pawn.Discarded && pawn.Faction == Faction.OfPlayer) return "CC_Nemesis_Status_Joined".Translate();
                    return "CC_Nemesis_Status_Gone".Translate();
                }
                if (captured) return "CC_Nemesis_Status_Captured".Translate();
                if (pawn != null && pawn.Spawned && pawn.Map.IsPlayerHome) return "CC_Nemesis_Status_Here".Translate();
                return "CC_Nemesis_Status_Loose".Translate();
            }
        }

        public string Line => "CC_Nemesis_Line".Translate(Name, DeedText, Status).Resolve();

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref victim, "victim");
            Scribe_Values.Look(ref victimName, "victimName");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref deed, "deed");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref announced, "announced");
            Scribe_Values.Look(ref onMap, "onMap", true);
            Scribe_Values.Look(ref returns, "returns");
            Scribe_Values.Look(ref captured, "captured");
            Scribe_Values.Look(ref ended, "ended");
            Scribe_Values.Look(ref endedTick, "endedTick");
            Scribe_Values.Look(ref endText, "endText");
            Scribe_Values.Look(ref lastSeedThreat, "lastSeedThreat");
            Scribe_Values.Look(ref lastSeen, "lastSeen");
        }
    }

    /// <summary>
    /// Enemies who hurt one of ours badly and lived. Raiders who flee are remembered, may ride with their people
    /// again, and can be captured or brought down at last; a wild animal that mauls someone and slinks off gets a
    /// name. The one they hurt feels it every step of the way.
    /// </summary>
    public class NemesisTracker : IExposable
    {
        public List<Nemesis> all = new List<Nemesis>();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public IEnumerable<Nemesis> Of(Pawn victim) => victim == null ? Enumerable.Empty<Nemesis>() : all.Where(n => n.victim == victim && n.announced);

        public Nemesis For(Pawn enemy) => enemy == null ? null : all.FirstOrDefault(n => n.pawn == enemy && !n.ended);

        public bool IsNemesis(Pawn enemy) => For(enemy) is Nemesis n && n.announced;

        public IEnumerable<Nemesis> Open => all.Where(n => n.announced && !n.ended);

        private static readonly string[] Severity = { "downed", "maimed", "killed" };

        [Unsaved] private bool unpinned;

        public void Hourly(CouncilGameComponent comp)
        {
            if (!S.nemeses)
            {
                // Switched off: let the world forget them.
                if (!unpinned)
                {
                    foreach (var n in all.Where(n => n.pawn != null)) Find.WorldPawns?.ForcefullyKeptPawns.Remove(n.pawn);
                    unpinned = true;
                }
                return;
            }
            unpinned = false;
            int now = Find.TickManager.TicksGame;
            Safe(() => Detect(comp, now));
            foreach (var n in all.Where(n => !n.ended).ToList()) Safe(() => Update(comp, n, now));
            if (S.nemesisReturns) Safe(() => SeedReturns(now));
            // Old stories fade from the list (but stay in the chronicle and journals).
            var old = all.Where(n => n.ended).OrderBy(n => n.endedTick).ToList();
            for (int i = 0; i < old.Count - 30; i++) all.Remove(old[i]);
        }

        private static void Safe(Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("nemeses", e);
            }
        }

        // ---------------- Who did it ----------------

        private static bool IsFoe(Pawn c) =>
            c != null && !c.Dead && c.RaceProps != null
            && ((c.RaceProps.Humanlike && c.Faction != null && !c.Faction.IsPlayer && c.Faction.HostileTo(Faction.OfPlayer) && People.HasInnerLife(c))
                || (c.RaceProps.Animal && c.Faction == null));

        /// <summary>The living enemy behind the victim's freshest wounds, from the combat log.</summary>
        private static Pawn Attacker(Pawn victim, int maxAge)
        {
            var hediffs = victim.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            foreach (var h in hediffs.Where(h => h.combatLogEntry != null && h.ageTicks <= maxAge).OrderBy(h => h.ageTicks).Take(8))
            {
                var entry = h.combatLogEntry.Target;
                if (entry == null) continue;
                // Only whoever struck: a colonist's stray shot names the raider it was aimed at as a concern too.
                var x = entry.GetConcerns().FirstOrDefault() as Pawn;
                if (x != null && x != victim && IsFoe(x)) return x;
            }
            return null;
        }

        /// <summary>The colonist who dealt the killing blow to a dead enemy, if the log says.</summary>
        private static Pawn Slayer(Pawn dead)
        {
            var hediffs = dead.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            foreach (var h in hediffs.Where(h => h.combatLogEntry != null).OrderBy(h => h.ageTicks).Take(5))
            {
                var entry = h.combatLogEntry.Target;
                var k = entry?.GetConcerns().FirstOrDefault() as Pawn;
                if (k != null && k != dead && k.Faction == Faction.OfPlayer && k.RaceProps.Humanlike) return k;
            }
            return null;
        }

        private void Record(Pawn enemy, Pawn victim, string deed, int now)
        {
            var n = For(enemy);
            if (n != null)
            {
                // The same enemy, worse: keep the worst thing they did (to the first they hurt, unless they killed someone since).
                if (Array.IndexOf(Severity, deed) > Array.IndexOf(Severity, n.deed))
                {
                    n.deed = deed;
                    if (deed == "killed")
                    {
                        n.victim = victim;
                        n.victimName = victim.LabelShort;
                    }
                }
                return;
            }
            all.Add(new Nemesis { pawn = enemy, victim = victim, victimName = victim.LabelShort, name = enemy.LabelShort, deed = deed, tick = now });
        }

        private void Detect(CouncilGameComponent comp, int now)
        {
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                foreach (var p in map.mapPawns.FreeColonistsSpawned.ToList())
                {
                    if (!People.HasInnerLife(p) || now - (p.mindState?.lastHarmTick ?? -999999) > GenDate.TicksPerHour * 2) continue;
                    string deed = null;
                    if (p.Downed) deed = "downed";
                    if (p.health.hediffSet.hediffs.Any(h => h.ageTicks < GenDate.TicksPerHour * 2 && (h is Hediff_MissingPart || h.IsPermanent()))) deed = "maimed";
                    if (deed == null) continue;
                    var x = Attacker(p, GenDate.TicksPerHour * 2);
                    if (x != null) Record(x, p, deed, now);
                }
            }
        }

        /// <summary>
        /// A colonist was just downed or killed by this attacker (from a Harmony hook, so it's exact; the hourly scan of
        /// the combat log remains for when the hooks are off).
        /// </summary>
        public void Notify_Harmed(Pawn victim, Pawn attacker, string deed)
        {
            if (!S.nemeses || victim == null || attacker == null || attacker == victim) return;
            if (!victim.IsColonist || !People.HasInnerLife(victim) || !IsFoe(attacker)) return;
            Record(attacker, victim, deed, Find.TickManager.TicksGame);
        }

        /// <summary>A colonist has died; if their killer lives, they become someone's nemesis.</summary>
        public void Notify_Death(Pawn dead)
        {
            if (!S.nemeses || dead == null) return;
            // The exact killer may already be known (Harmony hook): don't name a second one.
            if (all.Any(n => n.victim == dead && n.deed == "killed" && !n.ended)) return;
            var x = Attacker(dead, GenDate.TicksPerHour * 12);
            if (x != null) Record(x, dead, "killed", Find.TickManager.TicksGame);
        }

        // ---------------- What becomes of them ----------------

        private void Update(CouncilGameComponent comp, Nemesis n, int now)
        {
            var x = n.pawn;
            if (x == null || x.Discarded || (x.Destroyed && !x.Dead))
            {
                if (n.announced) End(n, "CC_Nemesis_End_Vanished".Translate().ToString());
                else all.Remove(n);
                return;
            }
            if (x.Dead)
            {
                if (n.announced) Slain(comp, n);
                else all.Remove(n);
                return;
            }
            // Tamed, recruited, joined: not an enemy any more.
            if (x.Faction == Faction.OfPlayer && !x.IsPrisonerOfColony)
            {
                if (n.announced) End(n, "CC_Nemesis_End_Joined".Translate(x.Named("X")).Resolve());
                else all.Remove(n);
                return;
            }
            // Someone they only knocked down has since died of other causes, or left: the grudge goes with them.
            bool victimHere = n.victim != null && !n.victim.Dead && n.victim.Faction == Faction.OfPlayer;
            if (!victimHere && n.deed != "killed" && !n.Sworn)
            {
                if (n.announced) End(n, null);
                else all.Remove(n);
                return;
            }
            if (x.IsPrisonerOfColony)
            {
                if (!n.captured && !n.announced)
                {
                    // Taken alive in the same fight: still the one who did it, now behind a door.
                    n.announced = true;
                    n.name = x.LabelShort;
                    n.tick = now;
                    Captured(comp, n);
                }
                else if (!n.captured) Captured(comp, n);
                n.captured = true;
                n.onMap = true;
                n.lastSeen = now;
                return;
            }
            n.captured = false;
            bool here = x.Spawned && x.Map.IsPlayerHome;
            if (!n.announced)
            {
                if (n.IsBeast)
                {
                    if (!x.Spawned)
                    {
                        all.Remove(n);
                        return;
                    }
                    // The fight is over and it's still out there, calm again: it gets a name.
                    if (!x.InMentalState && !comp.social.battleActive && !GenHostility.AnyHostileActiveThreatToPlayer(x.Map, false, false)) Escaped(comp, n, now);
                }
                // Off every map (fled, carried off, or the caravan's battlefield was left behind): they got away.
                else if (!x.Spawned && now - n.tick < GenDate.TicksPerDay * 2) Escaped(comp, n, now);
                else if (!x.Spawned) all.Remove(n);
                n.onMap = here;
                if (n.announced) n.lastSeen = now;
                return;
            }
            if (here)
            {
                if (!n.onMap)
                {
                    n.returns++;
                    Returned(comp, n);
                }
                n.lastSeen = now;
            }
            n.onMap = here;
            if (!n.IsBeast && !x.Spawned)
            {
                // Long gone and never seen again: let it end, and let the world have them back.
                if (now - n.lastSeen > GenDate.TicksPerDay * 120)
                {
                    End(n, "CC_Nemesis_End_Vanished".Translate().ToString());
                    return;
                }
                // Keep them in the world while they're still someone's nemesis (a return through a raid unpins them).
                if (Find.WorldPawns.Contains(x)) Find.WorldPawns.ForcefullyKeptPawns.Add(x);
            }
        }

        private static bool Bold(Pawn p)
        {
            var t = p.story?.traits;
            if (t == null) return false;
            var tough = DefDatabase<TraitDef>.GetNamedSilentFail("Tough");
            if ((tough != null && t.HasTrait(tough)) || t.HasTrait(TraitDefOf.Bloodlust)) return true;
            var nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
            return nerves != null && t.HasTrait(nerves) && t.DegreeOfTrait(nerves) > 0;
        }

        private static bool VictimCanFeel(Nemesis n) => n.victim != null && !n.victim.Dead && n.victim.IsColonist && People.HasInnerLife(n.victim);

        private void Escaped(CouncilGameComponent comp, Nemesis n, int now)
        {
            using var _cause = Causes.Push("CC_Cause_Nemesis".Translate(n.pawn?.LabelShort ?? n.name ?? "?", n.DeedText).Resolve());
            var x = n.pawn;
            n.announced = true;
            n.tick = now;
            if (n.IsBeast && x.Name == null)
            {
                string named = Flavor.FromPool("Council_Pool_BeastNames", n.victim ?? x, null, null,
                    x.RaceProps.predator ? "predator" : "grazer", x.RaceProps.baseBodySize >= 2f ? "huge" : "small");
                if (!named.NullOrEmpty()) x.Name = new NameSingle(named.Trim());
            }
            n.name = x.LabelShort;
            n.lastSeen = now;
            if (!n.IsBeast) Find.WorldPawns.ForcefullyKeptPawns.Add(x);
            string key = n.IsBeast ? "CC_Nemesis_EscapedBeast" : "CC_Nemesis_Escaped";
            string text = key.Translate(x.Named("X"), (n.victimName ?? "?").Named("VICTIM"), n.DeedText.Named("DEED")).Resolve();
            bool first = all.Count(o => o.announced && !o.ended && o.victim == n.victim) <= 1;
            if (first && (x.Spawned || n.victim?.MapHeld != null))
                Find.LetterStack.ReceiveLetter("CC_Nemesis_Label".Translate(x.LabelShort), text, LetterDefOf.NeutralEvent, x.Spawned ? new LookTargets(x) : new LookTargets(n.victim));
            else Notify.Message(text, x.Spawned ? new LookTargets(x) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, n.victim));
            if (VictimCanFeel(n))
            {
                CouncilGameComponent.GiveThought(n.victim, CouncilDefOf.Council_Nemesis_Escaped);
                comp.journals.Write(n.victim, n.IsBeast ? "Council_Journal_BeastEscaped" : "Council_Journal_NemesisEscaped", null, x.Named("X"));
                comp.aspirations.Notify_Event(n.victim, "nemesis");
            }
        }

        /// <summary>A scenario starts the game with an enemy who already knows the colony: known from the first day.</summary>
        public Nemesis Add(Pawn enemy, Pawn victim, string deed, bool captive)
        {
            if (!S.nemeses || enemy == null || victim == null) return null;
            int now = Find.TickManager.TicksGame;
            var n = new Nemesis
            {
                pawn = enemy, victim = victim, victimName = victim.LabelShort, name = enemy.LabelShort, deed = deed, tick = now,
                announced = true, captured = captive, onMap = enemy.Spawned, lastSeen = now
            };
            all.Add(n);
            if (!enemy.Spawned && Find.WorldPawns.Contains(enemy)) Find.WorldPawns.ForcefullyKeptPawns.Add(enemy);
            return n;
        }

        /// <summary>A world event sent this nemesis at the head of a raid: counted and felt once, and its own letter already told the story.</summary>
        public void Notify_LedRaid(CouncilGameComponent comp, Nemesis n)
        {
            if (n?.pawn == null || !n.pawn.Spawned) return;
            n.returns++;
            n.onMap = true;
            n.lastSeen = Find.TickManager.TicksGame;
            Returned(comp, n, false);
        }

        private void Returned(CouncilGameComponent comp, Nemesis n, bool tell = true)
        {
            using var _cause = Causes.Push("CC_Cause_Nemesis".Translate(n.Name, n.DeedText).Resolve());
            var x = n.pawn;
            string text = "CC_Nemesis_Returned".Translate(x.Named("X"), (n.victimName ?? "?").Named("VICTIM"), n.DeedText.Named("DEED")).Resolve();
            if (tell)
            {
                if (n.IsBeast) Notify.Message(text, x, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                else Find.LetterStack.ReceiveLetter("CC_Nemesis_ReturnedLabel".Translate(x.LabelShort), text, LetterDefOf.ThreatSmall, x);
            }
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, n.victim));
            if (!VictimCanFeel(n)) return;
            CouncilGameComponent.GiveThought(n.victim, Bold(n.victim) ? CouncilDefOf.Council_Nemesis_Defiant : CouncilDefOf.Council_Nemesis_Dread);
            comp.journals.Write(n.victim, "Council_Journal_NemesisReturned", null, x.Named("X"));
            comp.aspirations.Notify_Event(n.victim, "nemesis");
        }

        private void Captured(CouncilGameComponent comp, Nemesis n)
        {
            using var _cause = Causes.Push("CC_Cause_Nemesis".Translate(n.Name, n.DeedText).Resolve());
            var x = n.pawn;
            string text = "CC_Nemesis_Captured".Translate(x.Named("X"), (n.victimName ?? "?").Named("VICTIM"), n.DeedText.Named("DEED")).Resolve();
            Notify.Message(text, x, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, n.victim));
            if (!VictimCanFeel(n)) return;
            CouncilGameComponent.GiveThought(n.victim, CouncilDefOf.Council_Nemesis_Captured);
            comp.journals.Write(n.victim, "Council_Journal_NemesisCaptured", null, x.Named("X"));
        }

        private void Slain(CouncilGameComponent comp, Nemesis n)
        {
            using var _cause = Causes.Push("CC_Cause_Nemesis".Translate(n.Name, n.DeedText).Resolve());
            var x = n.pawn;
            var slayer = Slayer(x);
            bool revenge = slayer != null && slayer == n.victim;
            string text = revenge
                ? "CC_Nemesis_Revenge".Translate(x.Named("X"), n.victim.Named("PAWN"), n.DeedText.Named("DEED")).Resolve()
                : slayer != null
                    ? "CC_Nemesis_SlainBy".Translate(x.Named("X"), slayer.Named("SLAYER"), (n.victimName ?? "?").Named("VICTIM"), n.DeedText.Named("DEED")).Resolve()
                    : "CC_Nemesis_Dead".Translate(x.Named("X"), (n.victimName ?? "?").Named("VICTIM"), n.DeedText.Named("DEED")).Resolve();
            Find.LetterStack.ReceiveLetter("CC_Nemesis_EndLabel".Translate(n.Name), text, LetterDefOf.PositiveEvent, new LookTargets(new[] { slayer, n.victim }.Where(p => p != null && p.Spawned)));
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, slayer ?? n.victim));
            if (VictimCanFeel(n))
            {
                CouncilGameComponent.GiveThought(n.victim, revenge ? CouncilDefOf.Council_Nemesis_Revenge : CouncilDefOf.Council_Nemesis_Avenged);
                comp.journals.Write(n.victim, revenge ? "Council_Journal_NemesisRevenge" : "Council_Journal_NemesisAvenged", null, x.Named("X"), (slayer ?? n.victim).Named("OTHER"));
                comp.aspirations.Notify(n.victim, "nemesis");
            }
            if (slayer != null && !revenge && slayer.IsColonist && People.HasInnerLife(slayer))
            {
                CouncilGameComponent.GiveThought(slayer, CouncilDefOf.Council_Nemesis_Slayer);
                comp.journals.Write(slayer, "Council_Journal_NemesisSlayer", null, x.Named("X"), (n.victimName ?? "?").Named("VICTIM"));
            }
            End(n, text);
        }

        private void End(Nemesis n, string how)
        {
            n.ended = true;
            n.endedTick = Find.TickManager.TicksGame;
            n.endText = how;
            n.name = n.Name;
            if (n.pawn != null) Find.WorldPawns?.ForcefullyKeptPawns.Remove(n.pawn);
        }

        // ---------------- Coming back ----------------

        /// <summary>A raid from their people has just arrived: sometimes the one who got away rides with it.</summary>
        private void SeedReturns(int now)
        {
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
            {
                int threat = map.storyState.LastThreatBigTick;
                if (threat <= 0 || now - threat > GenDate.TicksPerHour * 3) continue;
                var raiders = map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Humanlike && p.Faction != null && !p.Faction.IsPlayer
                                                                      && p.Faction.HostileTo(Faction.OfPlayer) && !p.Downed && p.GetLord() != null).ToList();
                if (raiders.Count < 2) continue;
                foreach (var n in all.Where(n => n.announced && !n.ended && !n.IsBeast && n.returns < 3 && n.lastSeedThreat != threat).ToList())
                {
                    var x = n.pawn;
                    if (x == null || x.Spawned || x.Dead || x.Faction == null) continue;
                    var anchor = raiders.Where(r => r.Faction == x.Faction).RandomElementWithFallback();
                    if (anchor == null) continue;
                    n.lastSeedThreat = threat;
                    if (!Rand.Chance(0.35f)) continue;
                    if (!Find.WorldPawns.Contains(x) || x.Downed || x.IsPrisoner || People.Kidnapped(x) || x.GetCaravan() != null
                        || QuestUtility.IsReservedByQuestOrQuestBeingGenerated(x) || x.health.summaryHealth.SummaryHealthPercent < 0.6f) continue;
                    if (!CellFinder.TryFindRandomCellNear(anchor.Position, map, 6, c => c.Standable(map) && !c.Fogged(map) && c.GetFirstPawn(map) == null, out var cell, 30)) continue;
                    Find.WorldPawns.RemovePawn(x);
                    GenSpawn.Spawn(x, cell, map);
                    var lord = anchor.GetLord();
                    if (lord != null && lord.CanAddPawn(x)) lord.AddPawn(x);
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref all, "all", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (all == null) all = new List<Nemesis>();
                all.RemoveAll(n => n == null || (n.pawn == null && !n.ended));
            }
        }
    }
}
