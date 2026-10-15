using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A colonist's sworn vengeance on the faction that killed (or took) someone they loved, or the animal bonded to
    /// them. It ends in blood (three of them fall to the avenger), with captives taken in the same fights, with the
    /// one who was taken coming home, or with the avenger laying it down: in time, more easily for the kind, and more
    /// easily still once there's peace with the killers.
    /// </summary>
    public class Vendetta : IExposable
    {
        public Pawn avenger;
        public Faction faction;
        public string victim;
        /// <summary>The ThingID of the one lost (to know when someone taken comes home). Null in older saves.</summary>
        public string victimId;
        public bool kidnapped;
        /// <summary>Sworn for a bonded animal rather than a person.</summary>
        public bool pet;
        public int tick;
        public int kills;
        /// <summary>Of the kills, how many were taken alive instead.</summary>
        public int captives;
        public bool settled;
        public bool peaceNoticed;
        /// <summary>How it ended: blood, captive, home, forgiven or peace (null while open, or when it simply lapsed).</summary>
        public string how;

        public const int KillsToSettle = 3;

        /// <summary>Which kills pay this debt: people for people, machines for machines. Null: no vendetta possible.</summary>
        public static RecordDef KillRecord(Faction f)
        {
            if (f == null) return null;
            if (f.def.humanlikeFaction) return RecordDefOf.KillsHumanlikes;
            if (f == Faction.OfMechanoids) return RecordDefOf.KillsMechanoids;
            return null;
        }

        public bool Active => !settled && avenger != null && !avenger.Dead && faction != null && !faction.defeated;

        /// <summary>The key for its line on the Bonds page and the Story tab ({0} avenger, {1} faction, {2} the lost, {3} paid, {4} of).</summary>
        public string LineKey
        {
            get
            {
                if (!settled) return kidnapped ? "CC_Social_VendettaTaken" : "CC_Social_Vendetta";
                string key = "CC_Social_VendettaSettled_" + how;
                return how != null && key.CanTranslate() ? key : "CC_Social_VendettaSettled";
            }
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref avenger, "avenger");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref victim, "victim");
            Scribe_Values.Look(ref kidnapped, "kidnapped");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref kills, "kills");
            Scribe_Values.Look(ref settled, "settled");
            Scribe_Values.Look(ref peaceNoticed, "peaceNoticed");
            Scribe_Values.Look(ref victimId, "victimId");
            Scribe_Values.Look(ref pet, "pet");
            Scribe_Values.Look(ref captives, "captives");
            Scribe_Values.Look(ref how, "how");
        }
    }

    /// <summary>A eulogy to come, with what was known about the dead at the moment they died.</summary>
    public class PendingEulogy : IExposable
    {
        public Pawn dead;
        public int dueTick;
        public Faction killers;
        public Pawn speaker;
        public bool close;
        public bool rival;
        public string goal;

        public void ExposeData()
        {
            Scribe_References.Look(ref dead, "dead");
            Scribe_Values.Look(ref dueTick, "dueTick");
            Scribe_References.Look(ref killers, "killers");
            Scribe_References.Look(ref speaker, "speaker");
            Scribe_Values.Look(ref close, "close");
            Scribe_Values.Look(ref rival, "rival");
            Scribe_Values.Look(ref goal, "goal");
        }
    }

    /// <summary>
    /// Watches for deaths and kidnappings. The ones who loved the lost swear vengeance on whoever did it (and fight
    /// harder when that faction comes back), and the closest of them speaks at the grave.
    /// </summary>
    public class VendettaTracker : IExposable
    {
        public List<Vendetta> vendettas = new List<Vendetta>();
        public List<PendingEulogy> eulogies = new List<PendingEulogy>();
        private List<Pawn> known = new List<Pawn>();
        private int lastBattleTick = -999999;
        private Faction lastBattleFaction;
        [Unsaved] private Map lastBattleMap;
        [Unsaved] private Dictionary<Pawn, int> killsAtBattleStart = new Dictionary<Pawn, int>();
        [Unsaved] private Dictionary<Pawn, int> capturesAtBattleStart = new Dictionary<Pawn, int>();
        [Unsaved] private bool wasFighting;
        [Unsaved] private HashSet<Faction> warnedThisBattle = new HashSet<Faction>();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public IEnumerable<Vendetta> Of(Pawn p) => vendettas.Where(v => v.avenger == p && v.Active);

        /// <summary>An open vendetta of theirs against a people still hostile (the vendetta mood asks for every colonist).</summary>
        public bool AnyHostile(Pawn p)
        {
            if (p == null) return false;
            for (int i = 0; i < vendettas.Count; i++)
            {
                var v = vendettas[i];
                if (v.avenger == p && v.Active && v.faction.HostileTo(Faction.OfPlayer)) return true;
            }
            return false;
        }

        private static bool Eligible(Pawn p) => People.HasInnerLife(p) && p.IsColonist && !p.IsQuestLodger() && p.RaceProps.Humanlike && !p.DevelopmentalStage.Baby();

        public void Hourly(CouncilGameComponent comp)
        {
            var social = comp.social;
            int now = Find.TickManager.TicksGame;
            if (social.battleActive)
            {
                lastBattleTick = now;
                lastBattleFaction = social.battleFaction;
                lastBattleMap = social.battleMap;
            }
            WatchLosses(comp, now);
            if (S.vendettas) Battles(comp, social);
            foreach (var e in eulogies.Where(e => now >= e.dueTick).ToList())
            {
                eulogies.Remove(e);
                // Brought back (resurrection, a death refused): no grave to speak at.
                if (S.eulogies && e.dead != null && e.dead.Dead) Safe(() => Eulogy(comp, e));
            }
        }

        private static void Safe(Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("vendettas", e);
            }
        }

        // ---------------- Losses ----------------

        private void WatchLosses(CouncilGameComponent comp, int now)
        {
            var current = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Where(Eligible).Distinct().ToList();
            foreach (var p in known.ToList())
            {
                if (p == null)
                {
                    known.Remove(p);
                    continue;
                }
                // Away at an outpost, aboard a ship, in a vehicle: still ours, not a loss.
                if (current.Contains(p) || (Eligible(p) && People.StillOurs(p))) continue;
                known.Remove(p);
                // The ones who loved them: a loss that can change what they want from life.
                foreach (var f in PawnsFinder.AllMaps_FreeColonists.ToList().Where(f => f != p && Eligible(f) && Close(comp, f, p)))
                    comp.aspirations.Notify_Event(f, "loss");
                if (p.Dead)
                {
                    comp.nemeses.Notify_Death(p);
                    CalendarTracker.Notify_Death(p);
                    var killers = Killers(p, now);
                    if (killers != null && S.vendettas) Swear(comp, p, killers, false);
                    comp.legacy.Notify_Death(comp, p);
                    if (S.eulogies) eulogies.Add(Snapshot(comp, p, killers, now));
                }
                else if (S.vendettas)
                {
                    var taker = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.kidnapped != null && f.kidnapped.KidnappedPawnsListForReading.Contains(p));
                    if (taker != null) Swear(comp, p, taker, true);
                }
            }
            foreach (var p in current)
                if (!known.Contains(p)) known.Add(p);
        }

        /// <summary>
        /// Who killed them: the faction of whoever dealt their most recent wound (from the combat log), or, failing
        /// that, the enemy in a fight going on where they fell, if they died of fresh wounds.
        /// </summary>
        private Faction Killers(Pawn dead, int now)
        {
            var injuries = dead.health?.hediffSet?.hediffs.OfType<Hediff_Injury>().OrderBy(h => h.ageTicks).ToList();
            if (injuries == null || injuries.Count == 0 || injuries[0].ageTicks > GenDate.TicksPerHour * 12) return null;
            foreach (var h in injuries.Take(5))
            {
                var entry = h.combatLogEntry?.Target;
                if (entry == null) continue;
                // The one who struck (a stray shot also names the raider it was aimed at).
                var attacker = entry.GetConcerns().FirstOrDefault() as Pawn;
                if (attacker == null || attacker == dead) continue;
                if (attacker.Faction == null || attacker.Faction == Faction.OfPlayer) break;
                return attacker.Faction.HostileTo(Faction.OfPlayer) ? attacker.Faction : null;
            }
            var where = dead.Corpse?.MapHeld ?? dead.MapHeld;
            return now - lastBattleTick < GenDate.TicksPerHour * 4 && where != null && where == lastBattleMap ? lastBattleFaction : null;
        }

        /// <summary>Who is closest to the dead, and what to say, while their bonds and goals are still on record.</summary>
        private static PendingEulogy Snapshot(CouncilGameComponent comp, Pawn dead, Faction killers, int now)
        {
            var e = new PendingEulogy { dead = dead, killers = killers, dueTick = now + Rand.Range(GenDate.TicksPerHour * 10, GenDate.TicksPerHour * 20) };
            var speakers = PawnsFinder.AllMaps_FreeColonists.Where(Eligible).Where(p => !p.DevelopmentalStage.Juvenile() && p.relations != null).ToList();
            if (speakers.Count == 0) return e;
            float Closeness(Pawn p) => p.relations.OpinionOf(dead) + comp.social.Warmth(p, dead) + (p.GetRelations(dead).Any() ? 40f : 0f);
            e.speaker = speakers.OrderByDescending(Closeness).First();
            e.close = Closeness(e.speaker) >= 20f;
            e.rival = comp.social.Strength(e.speaker, dead, TieKind.Rivals) >= 20f;
            e.goal = comp.aspirations.all.FirstOrDefault(a => a.pawn == dead && !a.done)?.Goal;
            return e;
        }

        /// <summary>The ones closest to the lost swear vengeance.</summary>
        private void Swear(CouncilGameComponent comp, Pawn lostOne, Faction faction, bool kidnapped)
        {
            if (faction == Faction.OfPlayer || !faction.HostileTo(Faction.OfPlayer) || Vendetta.KillRecord(faction) == null) return;
            var avengers = PawnsFinder.AllMaps_FreeColonists.Where(Eligible).Where(p => !p.DevelopmentalStage.Juvenile() && Close(comp, p, lostOne)
                                                                                 && !vendettas.Any(v => v.avenger == p && v.faction == faction && v.Active)).ToList();
            Sworn(comp, avengers, lostOne, faction, kidnapped, false);
        }

        /// <summary>
        /// A bonded animal killed in a fight with a hostile people (the pets' farewell asks): the one it was bonded to
        /// swears on them too.
        /// </summary>
        public void Notify_PetKilled(CouncilGameComponent comp, Pawn pet, Pawn person)
        {
            if (!S.vendettas || pet == null || person == null || !Eligible(person) || person.DevelopmentalStage.Juvenile()) return;
            var faction = Killers(pet, Find.TickManager.TicksGame);
            if (faction == null || faction == Faction.OfPlayer || !faction.HostileTo(Faction.OfPlayer) || Vendetta.KillRecord(faction) == null) return;
            if (vendettas.Any(v => v.avenger == person && v.faction == faction && v.Active)) return;
            Sworn(comp, new List<Pawn> { person }, pet, faction, false, true);
        }

        private void Sworn(CouncilGameComponent comp, List<Pawn> avengers, Pawn lostOne, Faction faction, bool kidnapped, bool pet)
        {
            if (avengers.Count == 0) return;
            foreach (var p in avengers)
            {
                vendettas.Add(new Vendetta
                {
                    avenger = p, faction = faction, victim = lostOne.LabelShort, victimId = lostOne.ThingID, kidnapped = kidnapped, pet = pet,
                    tick = Find.TickManager.TicksGame
                });
                comp.journals.WriteTagged(p, kidnapped ? "Council_Journal_VendettaTaken" : "Council_Journal_VendettaSworn", pet ? new[] { "pet" } : null, null,
                    lostOne.Named("OTHER"), faction.Name.Named("FACTION"));
            }
            string names = TextUtil.JoinAnd(avengers.Select(a => a.LabelShort).ToList());
            var first = avengers[0];
            var tags = new List<string> { kidnapped ? "taken" : "killed" };
            if (pet) tags.Add("pet");
            if (avengers.Count > 1) tags.Add("many");
            if (faction == Faction.OfMechanoids) tags.Add("mech");
            if (!pet && first.relations != null)
            {
                var rel = first.GetRelations(lostOne).ToList();
                if (rel.Any(r => r.familyByBloodRelation)) tags.Add("kin");
                else if (rel.Any(r => r == PawnRelationDefOf.Spouse || r == PawnRelationDefOf.Lover || r == PawnRelationDefOf.Fiance)) tags.Add("partner");
                else tags.Add("friend");
            }
            string line = Flavor.FromPool("Council_Pool_VendettaSworn", first, null, null, tags.ToArray());
            string text = !line.NullOrEmpty()
                ? line.Formatted(names.Named("NAME"), lostOne.Named("OTHER"), faction.Name.Named("FACTION")).Resolve()
                : (kidnapped ? "CC_Vendetta_SwornTaken" : "CC_Vendetta_Sworn").Translate(names, lostOne.Named("LOST"), faction.Name.Named("FACTION")).Resolve();
            Notify.Message(text, new LookTargets(avengers), MessageTypeDefOf.NegativeEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Loss, text, first));
        }

        private static bool Close(CouncilGameComponent comp, Pawn p, Pawn lostOne)
        {
            if (p == lostOne || p.relations == null) return false;
            if (p.GetRelations(lostOne).Any(r => r.familyByBloodRelation || r == PawnRelationDefOf.Spouse || r == PawnRelationDefOf.Lover || r == PawnRelationDefOf.Fiance)) return true;
            if (comp.social.ties.Any(t => t.announced && t.Between(p, lostOne) && t.kind != TieKind.Rivals && t.strength >= 30f)) return true;
            return p.relations.OpinionOf(lostOne) >= 60;
        }

        // ---------------- Fights ----------------

        private void Battles(CouncilGameComponent comp, SocialWeb social)
        {
            using var _cause = Causes.Push("CC_Cause_VendettaBlood".Translate().Resolve());
            bool fighting = social.battleActive;
            var faction = social.battleFaction;
            if (fighting && !wasFighting)
            {
                killsAtBattleStart.Clear();
                capturesAtBattleStart.Clear();
                warnedThisBattle.Clear();
            }
            if (fighting && faction != null)
            {
                // Count from when they joined this fight (also after loading a save mid-battle).
                var record = Vendetta.KillRecord(faction);
                foreach (var v in vendettas.Where(v => v.Active && v.faction == faction && record != null))
                {
                    if (!killsAtBattleStart.ContainsKey(v.avenger)) killsAtBattleStart[v.avenger] = (int)(v.avenger.records?.GetValue(record) ?? 0f);
                    if (!capturesAtBattleStart.ContainsKey(v.avenger)) capturesAtBattleStart[v.avenger] = (int)(v.avenger.records?.GetValue(RecordDefOf.PeopleCaptured) ?? 0f);
                }
                // The banners they swore against: fury.
                var sworn = vendettas.Where(v => v.Active && v.faction == faction && v.avenger.Spawned && v.avenger.Map.IsPlayerHome).ToList();
                foreach (var v in sworn)
                {
                    var hediff = v.avenger.health?.hediffSet?.GetFirstHediffOfDef(CouncilDefOf.Council_Vendetta_Fury);
                    if (hediff == null) v.avenger.health?.AddHediff(CouncilDefOf.Council_Vendetta_Fury);
                }
                if (sworn.Count > 0 && warnedThisBattle.Add(faction))
                    Notify.Message("CC_Vendetta_Fury".Translate(TextUtil.JoinAnd(sworn.Select(v => v.avenger.LabelShort).Distinct().ToList()), faction.Name.Named("FACTION")).Resolve(),
                        new LookTargets(sworn.Select(v => v.avenger)), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
            if (!fighting && wasFighting && lastBattleFaction != null)
            {
                // Debts paid in this fight.
                foreach (var v in vendettas.Where(v => v.Active && v.faction == lastBattleFaction).ToList())
                {
                    var record = Vendetta.KillRecord(v.faction);
                    if (record == null || !killsAtBattleStart.TryGetValue(v.avenger, out int before)) continue;
                    int delta = Math.Max(0, (int)(v.avenger.records?.GetValue(record) ?? 0f) - before);
                    // Taken alive in the same fight counts too: a debt paid without a grave.
                    int taken = v.faction.def.humanlikeFaction && capturesAtBattleStart.TryGetValue(v.avenger, out int capBefore)
                        ? Math.Max(0, (int)(v.avenger.records?.GetValue(RecordDefOf.PeopleCaptured) ?? 0f) - capBefore) : 0;
                    if (delta + taken <= 0) continue;
                    v.kills += delta + taken;
                    v.captives += taken;
                    if (v.kills >= Vendetta.KillsToSettle) Settle(comp, v, false, v.captives > 0 ? "captive" : "blood");
                    else CouncilGameComponent.GiveThought(v.avenger, CouncilDefOf.Council_Vendetta_Blood);
                }
            }
            wasFighting = fighting;
        }

        /// <summary>
        /// It's over. <paramref name="how"/>: blood (three of them fell), captive (some were taken alive), home (the one
        /// taken came back), forgiven (laid down in time) or peace (laid down once the killers made peace).
        /// </summary>
        private void Settle(CouncilGameComponent comp, Vendetta v, bool quietly, string how = "blood")
        {
            using var _cause = Causes.Push("CC_Cause_VendettaSettled".Translate(v.faction?.Name ?? "?", v.victim ?? "?").Resolve());
            v.settled = true;
            if (quietly) return;
            v.how = how;
            bool paid = how == "blood" || how == "captive";
            var tags = new List<string> { how };
            if (v.pet) tags.Add("pet");
            if (v.kidnapped) tags.Add("taken");
            string line = Flavor.FromPool("Council_Pool_VendettaSettled", v.avenger, null, null, tags.ToArray());
            string text = !line.NullOrEmpty()
                ? line.Formatted(v.avenger.Named("PAWN"), (v.victim ?? "?").Named("LOST"), v.faction.Name.Named("FACTION")).Resolve()
                : "CC_Vendetta_Avenged".Translate(v.avenger.Named("PAWN"), v.victim.Named("LOST"), v.faction.Name.Named("FACTION")).Resolve();
            Notify.Message(text, v.avenger, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Loss, text, v.avenger));
            string journal = paid ? "Council_Journal_Avenged" : how == "home" ? "Council_Journal_VendettaHome" : "Council_Journal_VendettaForgiven";
            comp.journals.WriteTagged(v.avenger, journal, tags.ToArray(), null, v.victim.Named("LOST"), v.faction.Name.Named("FACTION"));
            if (paid)
            {
                CouncilGameComponent.GiveThought(v.avenger, CouncilDefOf.Council_Vendetta_Avenged);
                comp.aspirations.Notify(v.avenger, "avenged");
                return;
            }
            var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(how == "home" ? "Council_Vendetta_Home" : "Council_Vendetta_LetGo");
            if (thought != null) CouncilGameComponent.GiveThought(v.avenger, thought);
        }

        /// <summary>
        /// Laying it down: after a month at least, and not while the fighting is fresh. The kind do it sooner; the
        /// bloodthirsty and the cold never do; peace with the killers makes it far likelier.
        /// </summary>
        private bool LaysItDown(Vendetta v, bool hostile, int now)
        {
            if (now - v.tick < GenDate.TicksPerDay * 30) return false;
            if (lastBattleFaction == v.faction && now - lastBattleTick < GenDate.TicksPerDay * 15) return false;
            var t = v.avenger.story?.traits;
            if (t != null && (t.HasTrait(TraitDefOf.Bloodlust) || t.HasTrait(TraitDefOf.Psychopath))) return false;
            float chance = 0.008f;
            if (t != null && t.HasTrait(TraitDefOf.Kind)) chance *= 3f;
            if (t != null && t.HasTrait(TraitDefOf.Abrasive)) chance *= 0.5f;
            if (!hostile) chance *= 3f;
            if ((v.avenger.needs?.mood?.CurLevelPercentage ?? 0.5f) >= 0.7f) chance *= 1.5f;
            if (v.pet) chance *= 1.5f;
            return Rand.Chance(chance);
        }

        /// <summary>Someone who was carried off is one of ours again.</summary>
        private static bool BackHome(Vendetta v) =>
            v.kidnapped && !v.victimId.NullOrEmpty() && PawnsFinder.AllMaps_FreeColonists.Any(p => p.ThingID == v.victimId && !p.Dead);

        public void Daily(CouncilGameComponent comp)
        {
            vendettas.RemoveAll(v => v.avenger == null || v.avenger.Dead || v.avenger.Discarded || v.faction == null);
            foreach (var v in vendettas.Where(v => !v.settled).ToList())
            {
                // Their faction is gone, or it's been years.
                if (v.faction.defeated || Find.TickManager.TicksGame - v.tick > GenDate.TicksPerYear * 2)
                {
                    Settle(comp, v, !v.faction.defeated);
                    continue;
                }
                if (!S.vendettas) continue;
                bool hostile = v.faction.HostileTo(Faction.OfPlayer);
                if (v.avenger.IsColonist && BackHome(v))
                {
                    Settle(comp, v, false, "home");
                    continue;
                }
                if (v.avenger.IsColonist && LaysItDown(v, hostile, Find.TickManager.TicksGame))
                {
                    Settle(comp, v, false, hostile ? "forgiven" : "peace");
                    continue;
                }
                // Peace with the people who did it.
                if (!hostile && !v.peaceNoticed && v.avenger.IsColonist)
                {
                    v.peaceNoticed = true;
                    CouncilGameComponent.GiveThought(v.avenger, CouncilDefOf.Council_Vendetta_Betrayed, null, -1f, "CC_Cause_VendettaBetrayed".Translate(v.faction?.Name ?? "?").Resolve());
                    comp.standings.Change(v.avenger, -8f * S.PenaltyScale, "CC_Log_PeaceWithKillers".Translate(v.faction.Name));
                    comp.journals.Write(v.avenger, "Council_Journal_PeaceWithKillers", null, v.victim.Named("LOST"), v.faction.Name.Named("FACTION"));
                }
                else if (hostile) v.peaceNoticed = false;
            }
            while (vendettas.Count(v => v.settled) > 40) vendettas.Remove(vendettas.First(v => v.settled));
        }

        // ---------------- Eulogies ----------------

        private void Eulogy(CouncilGameComponent comp, PendingEulogy e)
        {
            using var _cause = Causes.Push("CC_Cause_Funeral".Translate(e.dead?.LabelShort ?? "?").Resolve());
            var dead = e.dead;
            var speakers = PawnsFinder.AllMaps_FreeColonists.Where(Eligible).Where(p => !p.DevelopmentalStage.Juvenile() && p.relations != null).ToList();
            if (speakers.Count == 0) return;
            float Closeness(Pawn p) => p.relations.OpinionOf(dead) + comp.social.Warmth(p, dead) + (p.GetRelations(dead).Any() ? 40f : 0f);
            // Chosen when they died, while their bonds were still on record; someone else if that person is gone too.
            bool close = e.close && e.speaker != null && speakers.Contains(e.speaker);
            var speaker = close ? e.speaker : speakers.OrderByDescending(Closeness).First();
            if (!close) close = Closeness(speaker) >= 20f;
            if (!close) speaker = speakers.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0).First();

            var tags = new List<string>();
            if (!close) tags.Add("stranger");
            if ((e.rival && speaker == e.speaker) || comp.social.Strength(speaker, dead, TieKind.Rivals) >= 20f) tags.Add("rival");
            if (speaker.GetRelations(dead).Any(r => r.familyByBloodRelation)) tags.Add("kin");
            if (speaker.GetRelations(dead).Any(r => r == PawnRelationDefOf.Spouse || r == PawnRelationDefOf.Lover || r == PawnRelationDefOf.Fiance)) tags.Add("partner");
            if (e.killers != null) tags.Add("battle");
            if (dead.DevelopmentalStage.Juvenile()) tags.Add("deadchild");
            else if (Flavor.HumanAge(dead) >= 60f) tags.Add("deadold");
            string title = comp.epithets.TitlesOf(dead);
            if (!title.NullOrEmpty()) tags.Add("titled");
            string goal = e.goal;
            if (goal.NullOrEmpty()) comp.aspirations.lastMourned.TryGetValue(dead, out goal);
            if (!goal.NullOrEmpty()) tags.Add("unfinished");
            float years = (dead.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) / GenDate.TicksPerYear;
            if (years >= 2f) tags.Add("longtime");

            string raw = Flavor.FromPool("Council_Eulogy", speaker, null, null, tags.ToArray());
            if (raw.NullOrEmpty()) return;
            string speech = raw.Formatted(speaker.Named("PAWN"), dead.Named("OTHER"), (title ?? "").Named("TITLE"), (goal ?? "").Named("GOAL"),
                Mathf.Max(1, Mathf.RoundToInt(years)).Named("YEARS"), (e.killers?.Name ?? "").Named("FACTION")).Resolve();
            string text = "CC_Eulogy_Text".Translate(speaker.Named("PAWN"), dead.Named("OTHER"), speech.Named("SPEECH")).Resolve();
            Find.LetterStack.ReceiveLetter("CC_Eulogy_Label".Translate(dead.LabelShort), text, LetterDefOf.NeutralEvent, speaker);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Loss, text, speaker));
            comp.journals.Write(speaker, "Council_Journal_GaveEulogy", null, dead.Named("OTHER"));
            CouncilRecords.Add(speaker, CouncilRecords.EulogiesGiven);
            CouncilGameComponent.GiveThought(speaker, CouncilDefOf.Council_GaveEulogy);
            foreach (var p in speakers.Where(p => p != speaker && p.relations.OpinionOf(dead) >= 20 && p.Spawned))
                CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_EulogyHeard);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref vendettas, "vendettas", LookMode.Deep);
            Scribe_Collections.Look(ref eulogies, "eulogies", LookMode.Deep);
            Scribe_Collections.Look(ref known, "known", LookMode.Reference);
            Scribe_Values.Look(ref lastBattleTick, "lastBattleTick", -999999);
            Scribe_References.Look(ref lastBattleFaction, "lastBattleFaction");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (vendettas == null) vendettas = new List<Vendetta>();
                vendettas.RemoveAll(v => v == null || v.avenger == null || v.faction == null);
                if (eulogies == null) eulogies = new List<PendingEulogy>();
                eulogies.RemoveAll(e => e == null || e.dead == null);
                if (known == null) known = new List<Pawn>();
                known.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>"A debt of blood": a small weight while a vendetta is open and the faction is still hostile.</summary>
    public class ThoughtWorker_Vendetta : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var comp = CouncilGameComponent.Instance;
            var s = ColonyCouncilMod.Settings;
            if (comp?.vendettas == null || !s.vendettas || !s.MoodEffects || s.PenaltyScale <= 0f) return ThoughtState.Inactive;
            return comp.vendettas.AnyHostile(p) ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }
}
