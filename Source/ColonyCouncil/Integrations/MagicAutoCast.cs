using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// What casting on their own keeps with the game: the colonists the player switched on with their button "Casts on
    /// their own" (off for everyone until then). Its pass runs every 250 ticks at Sched.RareMagicAutoCast, only with A
    /// RimWorld of Magic and while someone is switched on or a cast is still followed (Integrations/MagicAutoCast.cs). A
    /// save from before has nobody switched on.
    /// </summary>
    public class MagicAutoCastBook : GameComponent
    {
        /// <summary>
        /// The colonists switched on, by id, the oldest first: MagicAutoCastMath.MaxOn at most, and once a day those no
        /// longer the colony's free people (dead, gone, taken) are let go.
        /// </summary>
        public List<int> on = new List<int>();

        private HashSet<int> onSet;

        private static MagicAutoCastBook cached;
        private static Game cachedGame;

        public MagicAutoCastBook(Game game)
        {
        }

        public static MagicAutoCastBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<MagicAutoCastBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public bool IsOn(int id)
        {
            onSet ??= new HashSet<int>(on);
            return onSet.Contains(id);
        }

        /// <summary>The player's click on the button: switched on or off.</summary>
        public void Set(int id, bool value)
        {
            if (value && !IsOn(id))
            {
                on.Add(id);
                MagicAutoCastMath.Trim(on);
            }
            else if (!value)
            {
                on.Remove(id);
            }
            onSet = null;
        }

        /// <summary>Lets go of the ids that aren't the colony's free people any more (once a day).</summary>
        public void Prune(HashSet<int> ours)
        {
            on.RemoveAll(id => !ours.Contains(id));
            MagicAutoCastMath.Trim(on);
            onSet = null;
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            onSet = null;
            MagicAutoCast.Reset();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareMagicAutoCast)) return;
            if (on.Count == 0 && !MagicAutoCast.Busy || !PeoplesMagic.Loaded) return;
            using var clock = PassClock.Time(Pass.RareMagicAutoCast);
            try
            {
                MagicAutoCast.Pass(this, now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("casting on their own: the pass", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref on, "on", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                on ??= new List<int>();
                onSet = null;
            }
        }
    }

    /// <summary>
    /// A colonist casting A RimWorld of Magic's damage spells on their own (the player's wish, 28 November 2026;
    /// docs/magic-autocast.md; the rules in MagicAutoCastMath). The player switches a colonist on with their button; every
    /// 250 ticks (Sched.RareMagicAutoCast) each one switched on, up and able, is looked at:
    /// <list type="bullet">
    /// <item>On a hunt: the prey's first cast. A damage spell they know that is ready, keeps 30% of their mana or stamina,
    /// reaches the prey in sight and has nobody else near the prey or in the line opens the hunt, once per prey; never a
    /// fire spell, and Shadow Strike (first when it may) only on prey the game counts as safe. The hunt job is put back to
    /// follow the cast, and after it the hunt goes on: the prey again, or its body carried home.</item>
    /// <item>In a fight, drafted (with fire at will on, not while walking where they were sent) or not (fighting already,
    /// or with "attack" as their response to danger): the enemy they fight, else the nearest one attacking the colony that
    /// they see. The hardest-hitting spell
    /// that is ready, keeps the reserve, reaches, and has no friend in the line or near the target; Shadow Strike only on an
    /// enemy with no other enemy near it. At most one cast every 500 ticks; what they were doing is taken up again after.</item>
    /// </list>
    /// Everything is cast by the mod's own job (RomSpells.JobFor, as the mod casts for the AI), so its effects, costs,
    /// cooldowns and checks are its own; its own auto-cast marks on each spell are never touched. After Shadow Strike, which
    /// drafts a colonist and sends them at a target still standing, one who wasn't drafted is let go of that draft once
    /// the mod has brought them back, and one who was goes back to what they did. Without the mod nothing here runs.
    /// </summary>
    public static class MagicAutoCast
    {
        /// <summary>The setting is on (and the deeper hooks, which the button needs), and A RimWorld of Magic is loaded.</summary>
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.magicAutoCast ?? true) && PeoplesMagic.Loaded;

        /// <summary>A cast asked for, followed until it is over.</summary>
        private sealed class Cast
        {
            public Pawn caster, target;
            public Map map;
            public Job job;
            public string name;
            public int asked, overAt = -1;
            public bool hunt, strike, wasDrafted, killIncapped;
            public JobDef before;
        }

        /// <summary>One of the caster's abilities that Rimfolk uses.</summary>
        private struct Known
        {
            public RomSpells.Held held;
            public MagicAutoCastMath.Spell spell;
        }

        private static readonly List<Cast> pending = new List<Cast>();
        /// <summary>When each caster last cast on their own, by id (in memory: after a load they may cast at once).</summary>
        private static readonly Dictionary<int, int> lastCast = new Dictionary<int, int>();
        /// <summary>The prey that have had their opening cast, by id, and when (a day).</summary>
        private static readonly Dictionary<int, int> opened = new Dictionary<int, int>();
        /// <summary>The button's spells and icon for each colonist, worked out again every two seconds of game time.</summary>
        private static readonly Dictionary<int, (int at, string spells, Texture2D icon)> shown = new Dictionary<int, (int, string, Texture2D)>();

        public static void Reset()
        {
            pending.Clear();
            lastCast.Clear();
            opened.Clear();
            shown.Clear();
        }

        /// <summary>A cast asked for is still followed.</summary>
        public static bool Busy => pending.Count > 0;

        private static bool Waiting(Pawn p) => pending.Any(c => c.caster == p);

        public static void Pass(MagicAutoCastBook book, int now)
        {
            // Followed even with the setting just turned off, so nobody is left in the draft Shadow Strike gave them.
            if (pending.Count > 0) Follow(now);
            if (!On || !RomSpells.OwnReady || book.on.Count == 0) return;
            MagicAutoCastMath.Prune(opened, now);
            if (Sched.Every(now, Sched.DayTicks, Sched.RareMagicAutoCast))
                book.Prune(new HashSet<int>(PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Select(x => x.thingIDNumber)));
            foreach (var map in Find.Maps)
            {
                var colonists = map?.mapPawns?.FreeColonistsSpawned;
                if (colonists == null) continue;
                foreach (var p in colonists.ToList())
                {
                    if (!book.IsOn(p.thingIDNumber)) continue;
                    try
                    {
                        Consider(p, now, false, out _);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("casting on their own: a caster", e);
                    }
                }
            }
        }

        // ================= Who may cast, and what they know =================

        /// <summary>Why this colonist can't cast on their own now, or null.</summary>
        public static string WhyNot(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned) return "not on a map";
            if (!p.IsFreeNonSlaveColonist || !p.IsColonistPlayerControlled) return "not one of the colony's free people in the player's hands";
            if (p.Downed) return "downed";
            if (p.InMentalState) return "in a mental state";
            if (!RestUtility.Awake(p)) return "asleep";
            if (p.WorkTagIsDisabled(WorkTags.Violent)) return "incapable of violence";
            if (p.stances?.stunner?.Stunned == true) return "stunned";
            var j = p.CurJob;
            if (j != null && (j.ability != null || RomSpells.CastJobs.Contains(j.def))) return "casting already";
            // Another system casting for them (RWoM Core's cast ownership: a psycast's warmup, Ability AI): not cut in on.
            if (MagicModel.CastOwned(p)) return "another casting system has them now";
            if (Waiting(p)) return "a cast asked for is still under way";
            return null;
        }

        /// <summary>The abilities this colonist holds that Rimfolk uses (each paid from the pool its component keeps).</summary>
        private static List<Known> KnownBy(Pawn p)
        {
            var list = new List<Known>();
            foreach (var h in RomSpells.HeldBy(p))
            {
                var s = MagicAutoCastMath.Of(h.def.defName);
                if (s == null || (s.pays == MagicAutoCastMath.Pays.Stamina) != h.might) continue;
                list.Add(new Known { held = h, spell = s });
            }
            return list;
        }

        /// <summary>
        /// Looks at one colonist switched on: a hunt's opening cast or a cast in a fight. <paramref name="dry"/> only says
        /// what they would cast (the dev log); <paramref name="what"/> says what was cast or why not.
        /// </summary>
        public static bool Consider(Pawn p, int now, bool dry, out string what)
        {
            what = WhyNot(p);
            if (what != null) return false;
            var known = KnownBy(p);
            if (known.Count == 0)
            {
                what = "knows none of the damage spells used";
                return false;
            }
            return p.CurJobDef == JobDefOf.Hunt ? Hunt(p, known, now, dry, out what) : Fight(p, known, now, dry, out what);
        }

        // ================= On a hunt =================

        private static bool Hunt(Pawn p, List<Known> known, int now, bool dry, out string what)
        {
            if (!(p.CurJob?.targetA.Thing is Pawn prey) || prey.Dead || prey.Downed || !prey.Spawned || prey.Map != p.Map)
            {
                what = "hunting, the prey not up";
                return false;
            }
            if (MagicAutoCastMath.Opened(opened, prey.thingIDNumber, now))
            {
                what = "hunting " + prey.LabelShort + ", opened on already";
                return false;
            }
            return TryCast(p, known, prey, true, Safe(p, prey), false, now, dry, out what);
        }

        /// <summary>Prey Shadow Strike may be used on, by the game's own reading of how likely it turns on its hunter up close.</summary>
        private static bool Safe(Pawn hunter, Pawn prey)
        {
            float chance;
            try
            {
                chance = PawnUtility.GetManhunterOnDamageChance(prey, hunter, 1f);
            }
            catch
            {
                chance = float.NaN;
            }
            return MagicAutoCastMath.SafePrey(chance, prey.RaceProps?.predator ?? true, prey.RaceProps?.Humanlike ?? true, prey.BodySize, hunter.BodySize);
        }

        // ================= In a fight =================

        /// <summary>Jobs an undrafted colonist isn't taken from to cast: fleeing, rescuing, tending, eating, sleeping, a turret.</summary>
        private static bool Keeps(JobDef d) =>
            d == JobDefOf.Flee || d == JobDefOf.FleeAndCower || d == JobDefOf.FleeAndCowerShort || d == JobDefOf.Rescue || d == JobDefOf.Capture
            || d == JobDefOf.TendPatient || d == JobDefOf.Ingest || d == JobDefOf.LayDown || d == JobDefOf.ManTurret;

        private static bool Fight(Pawn p, List<Known> known, int now, bool dry, out string what)
        {
            if (lastCast.TryGetValue(p.thingIDNumber, out int last) && !MagicAutoCastMath.GapOver(last, now))
            {
                what = "cast a moment ago";
                return false;
            }
            var job = p.CurJob;
            bool fighting = job != null && (job.def == JobDefOf.AttackMelee || job.def == JobDefOf.AttackStatic);
            if (p.Drafted)
            {
                if (p.drafter != null && !p.drafter.FireAtWill)
                {
                    what = "drafted with fire at will off";
                    return false;
                }
                if (job?.def == JobDefOf.ManTurret)
                {
                    what = "manning a turret";
                    return false;
                }
                // Sent somewhere: the player's move stands (the mod's own drafted casting waits for it too).
                if (job?.def == JobDefOf.Goto)
                {
                    what = "drafted and on the way where they were sent";
                    return false;
                }
            }
            else if (!fighting)
            {
                if (p.playerSettings == null || p.playerSettings.hostilityResponse != HostilityResponseMode.Attack)
                {
                    what = "not drafted, not fighting, and their response to danger isn't to attack";
                    return false;
                }
                if (job != null && (job.playerForced || Keeps(job.def)))
                {
                    what = "busy with something not to break off (" + job.def.defName + ")";
                    return false;
                }
                if (p.carryTracker?.CarriedThing != null)
                {
                    what = "carrying something";
                    return false;
                }
            }
            var target = Target(p, known);
            if (target == null)
            {
                what = "no enemy fighting them or attacking the colony in sight and in range";
                return false;
            }
            return TryCast(p, known, target, false, false, Alone(p, target), now, dry, out what);
        }

        /// <summary>A pawn attacking the colony: hostile to them (raiders, manhunters, the berserk), up, and an active threat.</summary>
        private static bool Foe(Pawn p, Pawn x) =>
            x != null && x != p && x.Spawned && x.Map == p.Map && !x.Dead && !x.Downed && !x.IsPrisoner && x.HostileTo(p)
            && GenHostility.IsActiveThreatTo(x, Faction.OfPlayer, false, false);

        /// <summary>The enemy they fight (their attack's target, else the one their mind is set on), else the nearest attacking the colony that they see within their spells' range.</summary>
        private static Pawn Target(Pawn p, List<Known> known)
        {
            var job = p.CurJob;
            if (job != null && (job.def == JobDefOf.AttackMelee || job.def == JobDefOf.AttackStatic) && job.targetA.Thing is Pawn a && Foe(p, a)) return a;
            if (p.mindState?.enemyTarget is Pawn e && Foe(p, e)) return e;
            float reach = 0f;
            foreach (var k in known)
            {
                var v = RomSpells.VerbOf(k.held.ability);
                if (v?.verbProps != null) reach = Math.Max(reach, v.verbProps.range);
            }
            if (reach <= 0f) return null;
            Pawn best = null;
            float bestD = reach * reach;
            foreach (var t in p.Map.attackTargetsCache.TargetsHostileToColony)
            {
                if (!(t?.Thing is Pawn x) || !Foe(p, x)) continue;
                float d = (x.Position - p.Position).LengthHorizontalSquared;
                if (d > bestD || best != null && d >= bestD) continue;
                if (!GenSight.LineOfSight(p.Position, x.Position, p.Map)) continue;
                best = x;
                bestD = d;
            }
            return best;
        }

        /// <summary>A death knight's hate: the highest of the mod's hate hediffs on them (TM_HateHD and its levels), or 0.</summary>
        private static float Hate(Pawn p)
        {
            float most = 0f;
            var hediffs = p?.health?.hediffSet?.hediffs;
            if (hediffs == null) return most;
            foreach (var h in hediffs)
                if (h?.def?.defName != null && h.def.defName.StartsWith(MagicAutoCastMath.HateHediff, StringComparison.Ordinal))
                    most = Math.Max(most, h.Severity);
            return most;
        }

        /// <summary>No other enemy near the target (Shadow Strike lands its caster beside it).</summary>
        private static bool Alone(Pawn p, Pawn target)
        {
            float r2 = MagicAutoCastMath.StrikeAloneRadius * MagicAutoCastMath.StrikeAloneRadius;
            foreach (var x in p.Map.mapPawns.AllPawnsSpawned)
                if (x != target && Foe(p, x) && (x.Position - target.Position).LengthHorizontalSquared <= r2)
                    return false;
            return true;
        }

        // ================= The cast =================

        /// <summary>
        /// Nobody the spell shouldn't hit is within its reach of the target, or (unless it goes over the heads) on a cell of
        /// the line between: on a hunt nobody at all but the prey, in a fight nobody who isn't hostile to the colony.
        /// </summary>
        private static bool Clear(Pawn p, Pawn target, MagicAutoCastMath.Spell s, bool hunt)
        {
            var map = p.Map;
            bool Spare(Pawn x) => x != null && x != p && x != target && !x.Dead && (hunt || !x.HostileTo(Faction.OfPlayer));
            int r2 = s.reach * s.reach;
            foreach (var x in map.mapPawns.AllPawnsSpawned)
                if (Spare(x) && (x.Position - target.Position).LengthHorizontalSquared <= r2)
                    return false;
            if (s.overhead) return true;
            foreach (var c in GenSight.PointsOnLineOfSight(p.Position, target.Position))
            {
                if (c == p.Position || c == target.Position || !c.InBounds(map)) continue;
                var things = c.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                    if (things[i] is Pawn x && Spare(x))
                        return false;
            }
            return true;
        }

        private static bool InReach(Verb verb, Pawn target)
        {
            try
            {
                return verb != null && verb.CanHitTarget(target);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryCast(Pawn p, List<Known> known, Pawn target, bool hunt, bool safePrey, bool alone, int now, bool dry, out string what)
        {
            var options = new List<MagicAutoCastMath.Option>();
            var whys = new List<string>();
            foreach (var k in known)
            {
                string name = k.held.def.defName;
                if (hunt ? !MagicAutoCastMath.MayOnHunt(k.spell, safePrey) : !MagicAutoCastMath.MayInFight(k.spell, alone))
                {
                    whys.Add(name + ": " + (!hunt ? "another enemy near the target" : k.spell.fire ? "fire, never on a hunt" : "the prey isn't safe for it"));
                    continue;
                }
                if (!RomSpells.CanCast(k.held.ability))
                {
                    whys.Add(name + ": not ready (cooldown, cost or weapon)");
                    continue;
                }
                if (k.spell.name == MagicAutoCastMath.Spite && !MagicAutoCastMath.SpiteReady(MagicModel.InDiscipline(p, MagicAutoCastMath.KnightDiscipline), Hate(p)))
                {
                    whys.Add(name + ": not enough hate (a death knight of the class progression spends " + MagicAutoCastMath.SpiteHate + " on it)");
                    continue;
                }
                var pool = RomSpells.Pool(p, k.held.might);
                float cost = RomSpells.Cost(p, k.held.def, k.held.might);
                if (pool == null || !MagicAutoCastMath.KeepsReserve(pool.CurLevel, pool.MaxLevel, cost))
                {
                    whys.Add(name + ": would go below the reserve (" + (pool == null ? "no pool" : pool.CurLevel.ToString("0.00") + " of " + pool.MaxLevel.ToString("0.00") + ", costs " + cost.ToString("0.00")) + ")");
                    continue;
                }
                if (!InReach(RomSpells.VerbOf(k.held.ability), target))
                {
                    whys.Add(name + ": " + target.LabelShort + " out of range or sight");
                    continue;
                }
                if (!Clear(p, target, k.spell, hunt))
                {
                    whys.Add(name + ": someone in the way or near " + target.LabelShort);
                    continue;
                }
                options.Add(new MagicAutoCastMath.Option { name = name, ready = true, reserve = true, inReach = true, clear = true });
            }
            string pick = MagicAutoCastMath.Pick(options, hunt, safePrey, alone);
            if (pick == null)
            {
                what = (hunt ? "hunting " : "fighting ") + target.LabelShort + ", nothing to cast: " + (whys.Count == 0 ? "no spell" : string.Join("; ", whys));
                return false;
            }
            var chosen = known.First(k => k.held.def.defName == pick);
            what = (dry ? "would cast " : "casts ") + pick + " on " + target.LabelShort + (hunt ? " (the hunt's opening)" : "");
            return dry || Start(p, chosen, target, hunt, now, ref what);
        }

        /// <summary>The hunt job again for the same prey, as it was given.</summary>
        private static Job HuntAgain(Pawn prey, Job like)
        {
            var j = JobMaker.MakeJob(JobDefOf.Hunt, prey);
            if (like != null)
            {
                j.killIncappedTarget = like.killIncappedTarget;
                j.ignoreDesignations = like.ignoreDesignations;
                j.playerForced = like.playerForced;
            }
            return j;
        }

        /// <summary>
        /// Starts the mod's cast job at once. What they were doing comes back after it: the game puts a job that can wait
        /// back at the head of their queue; a hunt that can't is queued again in its place.
        /// </summary>
        private static bool Start(Pawn p, Known k, Pawn target, bool hunt, int now, ref string what)
        {
            var job = RomSpells.JobFor(k.held.ability, target);
            if (job == null)
            {
                what = k.held.def.defName + ": the mod gave no job to cast it";
                return false;
            }
            job.endIfCantShootTargetFromCurPos = true;
            var cur = p.CurJob;
            bool resume = cur?.def != null && cur.def.suspendable;
            Job again = null;
            if (hunt && !resume && cur != null)
            {
                again = HuntAgain(target, cur);
                p.jobs.jobQueue.EnqueueFirst(again, JobTag.Misc);
            }
            var c = new Cast
            {
                caster = p, target = target, map = p.Map, job = job, name = k.held.def.defName, asked = now, hunt = hunt,
                strike = k.spell.name == MagicAutoCastMath.ShadowStrike, wasDrafted = p.Drafted, before = cur?.def,
                killIncapped = hunt && cur != null && cur.killIncappedTarget
            };
            try
            {
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, resume, true, null, JobTag.Misc, false, false, null, false, true, false);
            }
            catch (Exception e)
            {
                if (again != null && p.jobs.jobQueue.Contains(again)) p.jobs.jobQueue.Extract(again);
                CouncilLog.ErrorOnce("casting on their own: starting the mod's cast", e);
                what = k.held.def.defName + ": the cast couldn't start";
                return false;
            }
            pending.Add(c);
            lastCast[p.thingIDNumber] = now;
            if (hunt) opened[target.thingIDNumber] = now;
            return true;
        }

        // ================= After the cast =================

        /// <summary>The casts asked for: once over, Shadow Strike's draft and melee order undone where they weren't wanted, and a hunt kept going.</summary>
        private static void Follow(int now)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var c = pending[i];
                var p = c.caster;
                if (p == null || p.Dead || p.Destroyed || !p.Spawned || p.Map != c.map)
                {
                    pending.RemoveAt(i);
                    continue;
                }
                bool atIt = p.CurJob == c.job;
                if (atIt || p.jobs?.jobQueue?.Contains(c.job) == true)
                {
                    if (now - c.asked <= MagicAutoCastMath.CastWaitTicks && now >= c.asked) continue;
                    // Too long: let go, so it isn't cast unlooked-for later.
                    if (atIt) p.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    else p.jobs.jobQueue.Extract(c.job);
                    pending.RemoveAt(i);
                    continue;
                }
                if (c.overAt < 0 || c.overAt > now) c.overAt = now;
                // Shadow Strike: the mod brings them back where they cast from a few seconds after the strike (its projectile
                // stays on the map until then); looked at again once that is done.
                if (c.strike && (now - c.overAt < MagicAutoCastMath.StrikeReturnTicks || StrikeUnderWay(c)) && now - c.overAt < 2 * MagicAutoCastMath.CastWaitTicks)
                    continue;
                pending.RemoveAt(i);
                try
                {
                    if (c.strike) AfterStrike(c);
                    if (c.hunt) HuntOn(c);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("casting on their own: after a cast", e);
                }
            }
        }

        private static ThingDef strikeDef;
        private static bool strikeLooked;

        /// <summary>The mod's Shadow Strike from this caster is still on the map (it brings them back when it ends).</summary>
        private static bool StrikeUnderWay(Cast c)
        {
            if (!strikeLooked)
            {
                strikeLooked = true;
                strikeDef = DefDatabase<ThingDef>.GetNamedSilentFail("Projectile_ShadowStrike");
            }
            if (strikeDef == null || c.map?.listerThings == null) return false;
            foreach (var t in c.map.listerThings.ThingsOfDef(strikeDef))
                if (t is Projectile pr && pr.Spawned && pr.Launcher == c.caster)
                    return true;
            return false;
        }

        /// <summary>
        /// After Shadow Strike: one the strike drafted (they weren't before, the mod did it to send them at a target still
        /// standing) is let go of the draft while still at its orders; one who was drafted goes back to what they did.
        /// </summary>
        private static void AfterStrike(Cast c)
        {
            var p = c.caster;
            var j = p.CurJob;
            bool meleeOnTarget = j != null && j.def == JobDefOf.AttackMelee && j.targetA.Thing == c.target;
            bool standingBy = j == null || j.def == JobDefOf.Wait_Combat;
            if (MagicAutoCastMath.LetGoOfDraft(true, c.wasDrafted, p.Drafted, meleeOnTarget || standingBy) && p.drafter != null)
                p.drafter.Drafted = false;
            else if (MagicAutoCastMath.EndTheModsMelee(true, c.wasDrafted, p.Drafted, meleeOnTarget, c.before == JobDefOf.AttackMelee))
                p.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        private static bool OnIt(Job j, Pawn prey) =>
            j != null && j.def == JobDefOf.Hunt && (j.targetA.Thing == prey || prey.Corpse != null && j.targetA.Thing == prey.Corpse);

        /// <summary>
        /// The hunt goes on after its opening cast, as the game's own hunt would: the prey again while it is still marked for
        /// hunting, or its body carried home (the game's hunt does that for a dead prey). Nothing if they are on it already.
        /// </summary>
        private static void HuntOn(Cast c)
        {
            var p = c.caster;
            var prey = c.target;
            if (prey == null || p.Drafted || p.Downed || p.InMentalState || !p.Spawned || p.Map != c.map) return;
            if (OnIt(p.CurJob, prey) || p.jobs.jobQueue.Any(q => OnIt(q?.job, prey))) return;
            var corpse = prey.Dead ? prey.Corpse : null;
            bool alive = !prey.Dead && prey.Spawned && prey.Map == p.Map && c.map.designationManager.DesignationOn(prey, DesignationDefOf.Hunt) != null;
            bool body = corpse != null && corpse.Spawned && corpse.Map == p.Map;
            if (!alive && !body) return;
            var job = JobMaker.MakeJob(JobDefOf.Hunt, prey);
            job.killIncappedTarget = c.killIncapped;
            var cur = p.CurJob;
            if (cur == null || cur.def.isIdle)
                p.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true, null, JobTag.Misc, false, false, null, false, true, true);
            else
                p.jobs.jobQueue.EnqueueLast(job, JobTag.Misc);
        }

        // ================= The button =================

        public const int GroupKey = 0x52466811;

        private static Texture2D fallbackIcon;

        /// <summary>The first of their spells' own icon, else the game's fire-at-will icon.</summary>
        private static Texture2D IconOf(List<Known> known)
        {
            foreach (var k in known)
            {
                try
                {
                    if (AccessTools.Field(k.held.def.GetType(), "uiIcon")?.GetValue(k.held.def) is Texture2D t && t != BaseContent.BadTex) return t;
                }
                catch
                {
                    // the next one, or the game's icon
                }
            }
            fallbackIcon ??= ContentFinder<Texture2D>.Get("UI/Commands/FireAtWill", false) ?? BaseContent.BadTex;
            return fallbackIcon;
        }

        /// <summary>
        /// The button "Casts on their own" on one of the colony's free colonists who knows one of the spells used, with the
        /// setting on; null otherwise. Its description names their spells and the rules.
        /// </summary>
        public static Gizmo GizmoFor(Pawn p)
        {
            if (!On || p == null || !p.IsColonistPlayerControlled || !p.IsFreeNonSlaveColonist || !RomSpells.OwnReady) return null;
            var book = MagicAutoCastBook.Instance;
            if (book == null) return null;
            int now = Find.TickManager?.TicksGame ?? 0;
            int id = p.thingIDNumber;
            if (!shown.TryGetValue(id, out var s) || now - s.at >= 120 || now < s.at)
            {
                var known = KnownBy(p);
                string spells = known.Count == 0 ? null : string.Join(", ", known.Select(k => k.held.def.LabelCap.ToString()).Distinct());
                s = (now, spells, spells == null ? null : IconOf(known));
                shown[id] = s;
            }
            if (s.spells == null) return null;
            return new Command_Toggle
            {
                defaultLabel = "CC_MagicAutoCast_Gizmo".Translate(),
                defaultDesc = "CC_MagicAutoCast_GizmoDesc".Translate(p.Named("PAWN"), s.spells.Named("SPELLS"), MagicAutoCastMath.Reserve.ToStringPercent().Named("RESERVE")),
                icon = s.icon,
                isActive = () => book.IsOn(id),
                toggleAction = () => book.Set(id, !book.IsOn(id)),
                groupKey = GroupKey
            };
        }
    }

    /// <summary>The button "Casts on their own" on a colonist who knows one of the damage spells used (off with the deeper hooks).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_MagicAutoCastGizmo
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;
            if (!RimfolkPatches.On) yield break;
            Gizmo c = null;
            try
            {
                c = MagicAutoCast.GizmoFor(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("casting on their own: the button", e);
            }
            if (c != null) yield return c;
        }
    }
}
