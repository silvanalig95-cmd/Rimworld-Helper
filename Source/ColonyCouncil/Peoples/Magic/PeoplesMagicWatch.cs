using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// What the peoples' magic keeps with the game (Peoples/Magic/PeoplesMagic.cs): the visitors of the peoples who have cast a
    /// spell for the colony this visit (each one at most once a visit; let go when they leave). Its pass, every 250 ticks
    /// at Sched.RarePeoplesMagic and only with A RimWorld of Magic, follows the peoples' casters on the colony's homes
    /// (<see cref="PeoplesMagicWatch"/>). A save from before has none of it.
    /// </summary>
    public class PeoplesMagicBook : GameComponent
    {
        /// <summary>Visitors of the peoples who cast for the colony this visit, by their id; dropped when they leave.</summary>
        public List<int> cast = new List<int>();

        private static PeoplesMagicBook cached;
        private static Game cachedGame;

        public PeoplesMagicBook(Game game)
        {
        }

        public static PeoplesMagicBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<PeoplesMagicBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            PeoplesMagicWatch.Reset();
            PeoplesMagic.Reset();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RarePeoplesMagic) || !PeoplesMagic.Loaded) return;
            using var clock = PassClock.Time(Pass.RarePeoplesMagic);
            try
            {
                PeoplesMagicWatch.Pass(this, now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' magic: the casters on the map", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref cast, "cast", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) cast ??= new List<int>();
        }
    }

    /// <summary>
    /// The peoples' casters on the colony's homes, every 250 ticks (Sched.RarePeoplesMagic):
    /// <list type="bullet">
    /// <item>Said aloud: a visitor of the peoples with a class names their craft to the first of the colony who is with them
    /// in their first hours on the map, and that colonist remarks on it, in turn over their heads
    /// (Rimfolk_Pool_MagicName, Rimfolk_Pool_MagicSeen); a raider's craft is named by a colonist who sees them
    /// (Rimfolk_Pool_MagicFoe), once a people a map every six hours.</item>
    /// <item>A spell in peace (on the pass's tick of the hour): a visitor of a class that has one (PeoplesMagicMath.PeaceSpells)
    /// who may (content on the map or of an allied people, no danger, not yet this visit) and rolls it (PeaceChance) casts
    /// it for whoever needs it, with the mod's own job, effects and mana: the most hurt colonist healed, an infection or
    /// a flu cured, an old scar eased, rain in a dry spell. When the mod shows it cast, the caster says a word, the one it
    /// was for (or someone out under the rain) answers, and a message tells it; a try the mod didn't carry out is let go
    /// unsaid.</item>
    /// </list>
    /// Nothing here but the book's list is saved: after a load the casters already there are taken as long arrived.
    /// </summary>
    public static class PeoplesMagicWatch
    {
        public const string NamePool = "Rimfolk_Pool_MagicName", SeenPool = "Rimfolk_Pool_MagicSeen", FoePool = "Rimfolk_Pool_MagicFoe",
            CastPool = "Rimfolk_Pool_MagicCast", ThanksPool = "Rimfolk_Pool_MagicThanks";

        /// <summary>When each caster on the homes was first seen this visit, by id.</summary>
        private static readonly Dictionary<int, int> seen = new Dictionary<int, int>();
        /// <summary>The casters who have named their craft this visit.</summary>
        private static readonly HashSet<int> named = new HashSet<int>();
        /// <summary>When a raider's craft was last named, by their people and the map.</summary>
        private static readonly Dictionary<string, int> foeSaid = new Dictionary<string, int>();
        /// <summary>Spells asked of a caster, waiting for the mod to cast them.</summary>
        private static readonly List<Cast> pending = new List<Cast>();
        /// <summary>The first pass since a load has looked (those there before it are taken as long arrived).</summary>
        private static bool primed;

        private static readonly List<string> dry = new List<string> { PeoplesMagicMath.DrySpell };

        private sealed class Cast
        {
            public Pawn caster, target;
            public Map map;
            public string trait, spell;
            public PeoplesMagicMath.Need need;
            public object ability;
            public Job job;
            public int asked;
            public bool started;
            public HediffDef illness;
            public string illnessLabel;
        }

        public static void Reset()
        {
            seen.Clear();
            named.Clear();
            foeSaid.Clear();
            pending.Clear();
            primed = false;
        }

        /// <summary>A spell asked of this caster is waiting to be cast.</summary>
        public static bool Waiting(Pawn x) => x != null && pending.Any(c => c.caster == x);

        public static void Pass(PeoplesMagicBook book, int now)
        {
            if (pending.Count > 0) Follow(book, now);
            bool hour = Sched.Hourly(now, Sched.RarePeoplesMagic);
            var present = new HashSet<int>();
            foreach (var map in Homes.All)
            {
                var pawns = map?.mapPawns?.AllHumanlikeSpawned;
                if (pawns == null) continue;
                for (int i = 0; i < pawns.Count; i++)
                {
                    var x = pawns[i];
                    string trait = PeoplesMagic.TheirCraft(x);
                    // A caster held by the colony is no visitor and no raider: nothing of theirs is said or cast.
                    if (trait == null || x.Dead || x.IsPrisoner || x.IsSlave) continue;
                    int id = x.thingIDNumber;
                    present.Add(id);
                    // Before the first look after a load: as if long arrived, so no one names their craft twice.
                    if (!seen.ContainsKey(id)) seen[id] = primed ? now : now - PeoplesMagicMath.NameWindowTicks - 1;
                    try
                    {
                        if (x.HostileTo(Faction.OfPlayer))
                        {
                            Foe(x, trait, map, now);
                            continue;
                        }
                        if (!named.Contains(id) && now - seen[id] <= PeoplesMagicMath.NameWindowTicks && Name(x, trait)) named.Add(id);
                        if (hour && Peace(book, x, trait, map)) TryCast(x, trait, now, out _);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("peoples' magic: a caster on the map", e);
                    }
                }
            }
            primed = true;
            // The gone: their visit is over.
            foreach (int id in seen.Keys.Where(k => !present.Contains(k)).ToList()) seen.Remove(id);
            named.RemoveWhere(id => !present.Contains(id));
            book.cast.RemoveAll(id => !present.Contains(id));
            while (book.cast.Count > PeoplesMagicMath.MaxCast) book.cast.RemoveAt(0);
            foreach (var key in foeSaid.Where(kv => now - kv.Value > PeoplesMagicMath.FoeGapTicks || kv.Value > now).Select(kv => kv.Key).ToList()) foeSaid.Remove(key);
        }

        // ================= Said aloud =================

        /// <summary>A line of one of the pools for a caster of this class, its craft filled in, or null.</summary>
        private static string Line(string pool, Pawn speaker, string trait, string need = null)
        {
            string line = Flavor.FromPool(pool, speaker, null, null, "cl:" + trait, need == null ? null : "need:" + need);
            if (line.NullOrEmpty()) return null;
            return line.Formatted(PeoplesMagic.Word(trait, false).Named("CRAFT"), PeoplesMagic.WordWithArticle(trait).Named("ACRAFT"),
                PeoplesMagic.Word(trait, true).Named("CRAFTS")).Resolve().CapitalizeFirst();
        }

        /// <summary>The caster names their craft to someone of the colony who is with them, who remarks on it. True when it began.</summary>
        private static bool Name(Pawn x, string trait)
        {
            if (!Speech.On || !Together.Able(x) || OutsiderTalk.Busy(x)) return false;
            var c = Together.Company(x);
            if (c == null || OutsiderTalk.Busy(c)) return false;
            string name = Line(NamePool, x, trait), remark = Line(SeenPool, c, trait);
            if (name == null || remark == null) return false;
            // together: Together.Company found the two together; the scene says each line only while they still are.
            return OutsiderTalk.Play(c, x, new List<(bool us, string text)> { (false, name), (true, remark) }) >= 0;
        }

        /// <summary>A colonist who sees a raider of the peoples with a class names it, once a people a map every six hours.</summary>
        private static void Foe(Pawn x, string trait, Map map, int now)
        {
            if (!Speech.On || x.Downed || x.Faction == null) return;
            string key = x.Faction.loadID + ":" + map.uniqueID;
            if (foeSaid.TryGetValue(key, out int at) && now - at < PeoplesMagicMath.FoeGapTicks && at <= now) return;
            foreach (var c in map.mapPawns.FreeColonistsSpawned)
            {
                if (!Together.Able(c) || c.DevelopmentalStage.Baby() || !c.Position.InHorDistOf(x.Position, PeoplesMagicMath.FoeSightRange)) continue;
                if (!GenSight.LineOfSight(c.Position, x.Position, map, true)) continue;
                string line = Line(FoePool, c, trait);
                if (line == null) return;
                if (Speech.Say(c, line)) foeSaid[key] = now;
                return;
            }
        }

        // ================= A spell in peace =================

        /// <summary>A visitor leaving the map (their group's duty to go).</summary>
        private static bool Leaving(Pawn x)
        {
            var duty = x.mindState?.duty?.def;
            return duty != null && (duty == DutyDefOf.ExitMapBest || duty == DutyDefOf.ExitMapRandom || duty == DutyDefOf.ExitMapBestAndDefendSelf
                                    || duty == DutyDefOf.ExitMapNearDutyTarget);
        }

        /// <summary>
        /// Whether this visitor may cast for the colony now and rolls it this hour (PeoplesMagicMath.MayCastInPeace,
        /// PeaceChance), with a class that has a spell for peace and the mod letting those not of the colony cast.
        /// </summary>
        private static bool Peace(PeoplesMagicBook book, Pawn x, string trait, Map map)
        {
            if (!PeoplesMagicMath.Helps(trait) || Waiting(x) || !RomSpells.Ready || !RomSpells.AiMayCast) return false;
            return MayNow(book, x, map, out _) && Rand.Chance(PeoplesMagicMath.PeaceChance(PeoplesMagicMath.BuiltOnMagic(Peoples.CallingOf(x.Faction))));
        }

        /// <summary>The rules, without the roll: may this visitor cast for the colony now (and why not).</summary>
        public static bool MayNow(PeoplesMagicBook book, Pawn x, Map map, out string why)
        {
            var f = x.Faction;
            bool hostile = f == null || x.HostileTo(Faction.OfPlayer);
            bool allied = f != null && f.PlayerRelationKind == FactionRelationKind.Ally;
            float mood = x.needs?.mood?.CurLevelPercentage ?? float.NaN;
            bool danger = map.dangerWatcher.DangerRating != StoryDanger.None;
            bool unable = !Together.Able(x) || x.IsPrisoner || x.IsSlave || x.CurJob != null && x.CurJob.playerForced || x.CurJobDef == JobDefOf.Ingest || OutsiderTalk.Busy(x);
            bool done = book != null && book.cast.Contains(x.thingIDNumber);
            bool leaving = Leaving(x);
            why = hostile ? "hostile" : done ? "cast this visit already" : danger ? "danger on the map" : unable ? "busy or unable" : leaving ? "leaving"
                : !allied && !(mood >= PeoplesMagicMath.ContentMood) ? "not content (mood " + mood.ToString("0.00") + ") and not allied" : null;
            return PeoplesMagicMath.MayCastInPeace(hostile, allied, mood, done, danger, unable, leaving);
        }

        /// <summary>Wounds that can still heal, added up.</summary>
        private static float Wounds(Pawn p)
        {
            float sum = 0f;
            foreach (var h in p.health.hediffSet.hediffs)
                if (h is Hediff_Injury && !h.IsPermanent())
                    sum += h.Severity;
            return sum;
        }

        private static bool Near(Pawn x, Pawn c) =>
            c != x && !c.Dead && c.Spawned && c.Map == x.Map && !c.InMentalState && x.Position.InHorDistOf(c.Position, PeoplesMagicMath.TargetRange)
            && x.CanReach(c, PathEndMode.Touch, Danger.Some);

        /// <summary>The colony's most hurt, near enough and reachable, or null.</summary>
        private static Pawn Hurt(Pawn x) =>
            x.Map.mapPawns.FreeColonistsSpawned.Where(c => c.health?.hediffSet != null && PeoplesMagicMath.HurtEnough(Wounds(c), c.health.hediffSet.BleedRateTotal > 0.01f) && Near(x, c))
                .OrderByDescending(Wounds).FirstOrDefault();

        /// <summary>The illness the druid's cure takes, on a colonist, or null.</summary>
        private static Hediff Illness(Pawn c) =>
            c.health?.hediffSet?.hediffs.FirstOrDefault(h => h?.def != null && h.Visible && PeoplesMagicMath.CuredAtFirst(h.def.defName));

        private static Pawn Sick(Pawn x) => x.Map.mapPawns.FreeColonistsSpawned.Where(c => Illness(c) != null && Near(x, c)).OrderByDescending(c => Illness(c).Severity).FirstOrDefault();

        private static bool Scarred(Pawn c) => c.health?.hediffSet?.hediffs.Any(h => h is Hediff_Injury && h.Visible && h.IsPermanent()) == true;

        private static Pawn Scar(Pawn x) => x.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(c => Scarred(c) && Near(x, c));

        /// <summary>The ice mage's rain is wanted on this map (PeoplesMagicMath.RainWanted).</summary>
        public static bool RainWanted(Map map)
        {
            var w = map.weatherManager?.curWeather;
            bool falling = w != null && (w.rainRate > 0f || w.snowRate > 0f);
            bool heat = map.gameConditionManager.ConditionIsActive(GameConditionDefOf.HeatWave);
            int fires = 0;
            foreach (var t in map.listerThings.ThingsOfDef(ThingDefOf.Fire))
                if (t.Spawned && !map.roofGrid.Roofed(t.Position) && ++fires >= 3)
                    break;
            bool drySpell = WeatherBook.Of(map)?.Fits(dry) == true;
            return PeoplesMagicMath.RainWanted(falling, map.mapTemperature.OutdoorTemp, heat, fires >= 3, drySpell);
        }

        /// <summary>
        /// The caster casts the first of their class's spells for peace that has someone or something to cast for and
        /// that the mod says they can cast now, by the mod's own job (it walks into range and casts). True when the job
        /// began; <paramref name="why"/> says why not.
        /// </summary>
        public static bool TryCast(Pawn x, string trait, int now, out string why)
        {
            why = null;
            var spells = PeoplesMagicMath.PeaceSpells(trait);
            if (spells.Count == 0)
            {
                why = "no spell for peace in this class";
                return false;
            }
            var whys = new List<string>();
            foreach (var (need, spell) in spells)
            {
                var ability = RomSpells.Ability(x, spell);
                if (ability == null)
                {
                    whys.Add(spell + ": not known");
                    continue;
                }
                if (!RomSpells.CanCast(ability))
                {
                    whys.Add(spell + ": not now (mana or cooldown)");
                    continue;
                }
                Pawn target = null;
                Hediff illness = null;
                switch (need)
                {
                    case PeoplesMagicMath.Need.Heal:
                        target = Hurt(x);
                        break;
                    case PeoplesMagicMath.Need.Cure:
                        target = Sick(x);
                        illness = target == null ? null : Illness(target);
                        break;
                    case PeoplesMagicMath.Need.Scar:
                        target = Scar(x);
                        break;
                    case PeoplesMagicMath.Need.Rain:
                        if (!RainWanted(x.Map))
                        {
                            whys.Add(spell + ": no dry spell, heat wave or fire, or it is raining or freezing");
                            continue;
                        }
                        break;
                }
                if (need != PeoplesMagicMath.Need.Rain && target == null)
                {
                    whys.Add(spell + ": no one of the colony needs it near");
                    continue;
                }
                var job = RomSpells.JobFor(ability, need == PeoplesMagicMath.Need.Rain ? (LocalTargetInfo)x : target);
                if (job == null || !x.jobs.TryTakeOrderedJob(job, JobTag.Misc))
                {
                    whys.Add(spell + ": the mod's job couldn't start");
                    continue;
                }
                pending.Add(new Cast
                {
                    caster = x, target = target, map = x.Map, trait = trait, spell = spell, need = need, ability = ability, job = job, asked = now,
                    illness = illness?.def, illnessLabel = illness?.LabelBase
                });
                return true;
            }
            why = string.Join("; ", whys);
            return false;
        }

        /// <summary>The spells asked for: told once the mod shows them cast (the ability on its cooldown, the job over), else let go.</summary>
        private static void Follow(PeoplesMagicBook book, int now)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var c = pending[i];
                bool gone = c.caster == null || c.caster.Dead || !c.caster.Spawned || c.caster.Map != c.map
                            || c.target != null && (c.target.Dead || !c.target.Spawned || c.target.Map != c.map);
                bool atIt = !gone && c.caster.CurJob == c.job;
                if (atIt) c.started = true;
                // At it, or still waiting its turn behind a job that couldn't be broken off.
                if (!gone && (atIt || c.caster.jobs?.jobQueue?.Contains(c.job) == true))
                {
                    if (now - c.asked <= PeoplesMagicMath.CastWaitTicks) continue;
                    // Too long (stuck on the way): the try is let go, the job ended or taken back so it isn't cast unseen.
                    if (atIt) c.caster.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    else c.caster.jobs?.jobQueue?.Extract(c.job);
                    gone = true;
                }
                pending.RemoveAt(i);
                if (gone) continue;
                // The job is over (or was over before the first look): cast if the mod put the ability on its cooldown.
                if (RomSpells.Cooldown(c.ability) > 0 && (c.started || now - c.asked <= 2 * Sched.RareTicks)) Landed(book, c);
            }
        }

        /// <summary>
        /// A spell cast for the colony: the caster's once this visit; what it did told as the map shows it (a cure that
        /// didn't take is told as tried), a word from the caster and from the one it was for, or from a colonist out under
        /// the new rain.
        /// </summary>
        private static void Landed(PeoplesMagicBook book, Cast c)
        {
            if (!book.cast.Contains(c.caster.thingIDNumber)) book.cast.Add(c.caster.thingIDNumber);
            string key, need;
            switch (c.need)
            {
                case PeoplesMagicMath.Need.Heal:
                    key = "CC_PeoplesMagic_Did_Heal";
                    need = "heal";
                    break;
                case PeoplesMagicMath.Need.Cure:
                    bool cured = c.illness != null && c.target.health.hediffSet.GetFirstHediffOfDef(c.illness) == null;
                    key = cured ? "CC_PeoplesMagic_Did_Cure" : "CC_PeoplesMagic_Did_CureTried";
                    need = cured ? "cure" : null;
                    break;
                case PeoplesMagicMath.Need.Scar:
                    key = "CC_PeoplesMagic_Did_Scar";
                    need = "scar";
                    break;
                default:
                    var w = c.map.weatherManager?.curWeather;
                    // The rain the words speak of is falling (the mod's Rainmaker changes the weather at once).
                    if (w == null || w.rainRate <= 0f) return;
                    key = "CC_PeoplesMagic_Did_Rain";
                    need = "rain";
                    break;
            }
            Shown(c, key, need);
        }

        /// <summary>The message of what the spell did, and the words over the caster's and the answerer's heads.</summary>
        private static void Shown(Cast c, string key, string need)
        {
            var f = c.caster.Faction;
            // together: the spell went from one to the other on the map (the mod's own casting, its effect seen on them).
            string text = key.Translate(c.caster.Named("CASTER"), (c.target ?? c.caster).Named("TARGET"), PeoplesMagic.WordWithArticle(c.trait).Named("ACRAFT"),
                (f?.Name ?? "").Named("PEOPLE"), (c.illnessLabel ?? "").Named("ILLNESS")).Resolve();
            Tell.Message(NewsKind.World, Importance.Notable, c.map, text, new LookTargets(c.target ?? c.caster), MessageTypeDefOf.PositiveEvent);
            if (need == null) return;
            string said = Line(CastPool, c.caster, c.trait, need);
            if (said != null && Together.Able(c.caster)) Speech.Say(c.caster, said);
            Pawn answer = c.need == PeoplesMagicMath.Need.Rain ? UnderTheRain(c.map, c.caster) : c.target;
            if (answer == null || !Together.Able(answer) || answer.DevelopmentalStage.Baby()) return;
            string thanks = Line(ThanksPool, answer, c.trait, need);
            if (thanks != null) Speech.Say(answer, thanks);
        }

        /// <summary>A colonist out under the sky, up and about, the nearest the caster.</summary>
        private static Pawn UnderTheRain(Map map, Pawn near) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => Together.Able(p) && !map.roofGrid.Roofed(p.Position))
                .OrderBy(p => p.Position.DistanceToSquared(near.Position)).FirstOrDefault();
    }
}
