using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A raid the colony met, as the weapon suggestions remember it: when, how many, how many mechanoids and armoured raiders, a siege, a tribe.</summary>
    public class WeaponRaidNote : IExposable
    {
        public int tick, count, mechs, armoured;
        public bool siege, tribal;

        public RaidSeen Seen => new RaidSeen { Tick = tick, Count = count, Mechs = mechs, Armoured = armoured, Siege = siege, Tribal = tribal };

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref mechs, "mechs");
            Scribe_Values.Look(ref armoured, "armoured");
            Scribe_Values.Look(ref siege, "siege");
            Scribe_Values.Look(ref tribal, "tribal");
        }
    }

    /// <summary>A colonist's last ask for a better weapon: when, which weapon (its id), its words for the inbox; a trade's other (their id, else -1).</summary>
    public class WeaponAskNote : IExposable
    {
        public int pawn = -1, tick = -1, weapon = -1, other = -1;
        public string label, place;
        public float gain;

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawn, "pawn", -1);
            Scribe_Values.Look(ref tick, "tick", -1);
            Scribe_Values.Look(ref weapon, "weapon", -1);
            Scribe_Values.Look(ref other, "other", -1);
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref place, "place");
            Scribe_Values.Look(ref gain, "gain");
        }
    }

    /// <summary>
    /// What the weapon suggestions keep with the game (docs/design/weapons.md, step 3): the raids the colony met lately (the
    /// last <see cref="MaxRaids"/>, for the fights the suggestions weigh), the colonists whose suggestions the player set
    /// aside ("not now", for <see cref="WeaponAskMath.NotNowDays"/> days) and each colonist's last ask (one each), the
    /// colony's last ask, and when each colonist last filled an empty slot of their kit themselves (a day, pruned daily).
    /// Its clock: every 250 ticks at <see cref="Sched.RareWeaponPicks"/>, a tenth of the colonists each time (each once an
    /// hour): their ratings of the colony's weapons worked out ahead, an empty slot filled themselves (WeaponKitSelf), else
    /// their look for a weapon to ask for. A save from before has an empty one.
    /// </summary>
    public class WeaponPickBook : GameComponent
    {
        public const int MaxRaids = 12;

        public List<WeaponRaidNote> raids = new List<WeaponRaidNote>();
        /// <summary>Colonists' suggestions set aside (thingIDNumber: until when).</summary>
        public Dictionary<int, int> notNow = new Dictionary<int, int>();
        public List<WeaponAskNote> asks = new List<WeaponAskNote>();
        public int lastAsk = -1;
        /// <summary>When each colonist last went to fill an empty slot of their kit themselves (thingIDNumber: tick), for a day.</summary>
        public Dictionary<int, int> fetched = new Dictionary<int, int>();

        private static WeaponPickBook cached;
        private static Game cachedGame;

        public WeaponPickBook(Game game)
        {
        }

        public static WeaponPickBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<WeaponPickBook>();
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
            WeaponPick.Reset();
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref raids, "raids", LookMode.Deep);
            Scribe_Collections.Look(ref notNow, "notNow", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref asks, "asks", LookMode.Deep);
            Scribe_Values.Look(ref lastAsk, "lastAsk", -1);
            Scribe_Collections.Look(ref fetched, "fetched", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                raids ??= new List<WeaponRaidNote>();
                notNow ??= new Dictionary<int, int>();
                asks ??= new List<WeaponAskNote>();
                fetched ??= new Dictionary<int, int>();
                raids.RemoveAll(r => r == null);
                asks.RemoveAll(a => a == null || a.pawn < 0);
            }
        }

        // ---------------- Not now ----------------

        /// <summary>Until when a colonist's suggestions are set aside (0: they aren't).</summary>
        public int NotNowUntil(Pawn p) => p != null && notNow.TryGetValue(p.thingIDNumber, out var until) ? until : 0;

        public bool IsNotNow(Pawn p) => NotNowUntil(p) > Now;

        /// <summary>The player sets a colonist's suggestions aside for a while (they don't ask either), or brings them back.</summary>
        public void SetNotNow(Pawn p, bool aside)
        {
            if (p == null) return;
            if (aside) notNow[p.thingIDNumber] = Now + WeaponAskMath.NotNowDays * GenDate.TicksPerDay;
            else notNow.Remove(p.thingIDNumber);
            WeaponPick.Dirty();
        }

        // ---------------- A kit filled themselves ----------------

        /// <summary>Whether a colonist may go and fill an empty slot themselves now: not within a day of the last time.</summary>
        public bool MayFetch(Pawn p, int now) => p != null && (!fetched.TryGetValue(p.thingIDNumber, out var last) || now - last >= WeaponKitSelf.EachTicks || now < last);

        public void Fetched(Pawn p, int now)
        {
            if (p != null) fetched[p.thingIDNumber] = now;
        }

        // ---------------- Asks ----------------

        public WeaponAskNote AskOf(Pawn p) => p == null ? null : asks.FirstOrDefault(a => a.pawn == p.thingIDNumber);

        /// <summary>A colonist asked for a weapon (lying somewhere), or proposed a trade to <paramref name="other"/> for the weapon they hold.</summary>
        public void Asked(Pawn p, Thing weapon, string place, float gain, Pawn other = null)
        {
            var a = AskOf(p);
            if (a == null) asks.Add(a = new WeaponAskNote { pawn = p.thingIDNumber });
            a.tick = Now;
            a.weapon = weapon.thingIDNumber;
            a.other = other?.thingIDNumber ?? -1;
            a.label = weapon.LabelNoCount;
            a.place = place;
            a.gain = gain;
            lastAsk = Now;
        }

        // ---------------- The raids met ----------------

        /// <summary>A raid has come to one of the colony's homes: how many, mechanoids, armoured (flak or better on the body), a siege, a tribe's.</summary>
        public void Raided(IncidentParms parms, List<Pawn> pawns)
        {
            if (parms == null || pawns == null || pawns.Count == 0 || !(parms.target is Map map) || !Homes.IsHome(map)) return;
            if (!pawns.Any(p => p != null && p.Spawned)) return;
            var f = parms.faction;
            var note = new WeaponRaidNote
            {
                tick = Now, count = pawns.Count, mechs = pawns.Count(p => p?.RaceProps != null && p.RaceProps.IsMechanoid),
                armoured = pawns.Count(Armoured), siege = parms.raidStrategy?.defName == "Siege",
                tribal = f?.def != null && f.def.techLevel <= TechLevel.Neolithic && f.def.humanlikeFaction
            };
            raids.Add(note);
            if (raids.Count > MaxRaids) raids.RemoveRange(0, raids.Count - MaxRaids);
            situationsAt = -1;
        }

        /// <summary>Flak or better on the body: a piece worn on the torso with sharp armour of 0.4 or more (a flak vest's 1.0, marine armour's 1.06).</summary>
        private static bool Armoured(Pawn p)
        {
            if (p?.apparel == null || p.RaceProps.IsMechanoid) return false;
            foreach (var a in p.apparel.WornApparel)
            {
                var groups = a?.def?.apparel?.bodyPartGroups;
                if (groups == null || !groups.Any(g => g == BodyPartGroupDefOf.Torso)) continue;
                if (a.GetStatValue(StatDefOf.ArmorRating_Sharp) >= 0.4f) return true;
            }
            return false;
        }

        private Dictionary<string, float> situations;
        private int situationsAt = -1;

        /// <summary>
        /// The situations the colony meets (WeaponPickMath.ColonySituations), worked out once an hour: Rimfolk's own record
        /// of the raids, and the game's own raid letters for those it doesn't have (before this record, a mechanoid cluster,
        /// another mod's raid), each read by its people (mechanoids, a tribe's crowd, the armour of their age); a defense
        /// line on a home map is a gate the colony holds.
        /// </summary>
        public Dictionary<string, float> Situations()
        {
            int now = Now;
            if (situations != null && situationsAt >= 0 && now - situationsAt < GenDate.TicksPerHour) return situations;
            situationsAt = now;
            var seen = raids.Select(r => r.Seen).ToList();
            try
            {
                var archive = Find.Archive?.ArchivablesListForReading;
                if (archive != null)
                    foreach (var item in archive)
                    {
                        if (!(item is Letter l) || l.def != LetterDefOf.ThreatBig || l.relatedFaction?.def == null) continue;
                        if (WeaponPickMath.Recency(now, l.arrivalTick) <= 0f) continue;
                        if (raids.Any(r => Math.Abs(r.tick - l.arrivalTick) <= GenDate.TicksPerHour)) continue;
                        var fd = l.relatedFaction.def;
                        bool mech = !fd.humanlikeFaction;
                        seen.Add(new RaidSeen
                        {
                            Tick = l.arrivalTick, FromLetter = true, Mechs = mech ? 1 : 0, Tribal = !mech && fd.techLevel <= TechLevel.Neolithic,
                            ArmouredShare = mech ? 0f : fd.techLevel >= TechLevel.Spacer ? 0.7f : fd.techLevel == TechLevel.Industrial ? 0.4f : fd.techLevel == TechLevel.Medieval ? 0.2f : 0f
                        });
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the weapon suggestions reading the game's raid letters", e);
            }
            bool gate = false;
            foreach (var map in Homes.All)
                if (DefenseLines.Of(map)?.lines?.Count > 0) gate = true;
            situations = WeaponPickMath.ColonySituations(seen, now, gate);
            return situations;
        }

        /// <summary>How many raids the suggestions read now, and from where: for the colony's view.</summary>
        public int RaidsRead => raids.Count(r => WeaponPickMath.Recency(Now, r.tick) > 0f);

        // ---------------- The clock ----------------

        public override void GameComponentTick()
        {
            var tm = Find.TickManager;
            if (tm == null) return;
            int now = tm.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareWeaponPicks)) return;
            if (!WeaponPick.On && !WeaponAsks.On && !WeaponKitSelf.On) return;
            int slice = Sched.SliceTurn(now, Sched.HourTicks, Sched.Slices, Sched.RareWeaponPicks);
            if (slice < 0) return;
            using (PassClock.Time(Pass.RareWeaponPicks))
            {
                try
                {
                    foreach (var map in Homes.All)
                    {
                        var colonists = WeaponPick.Colonists(map).Where(p => Sched.SliceOf(p.thingIDNumber, Sched.Slices) == slice).ToList();
                        if (colonists.Count == 0) continue;
                        var picks = WeaponPick.Current(map);
                        foreach (var p in colonists) WeaponPick.Warm(picks, p);
                        if (!WeaponAsks.On && !WeaponKitSelf.On) continue;
                        picks = WeaponPick.For(map);
                        // An empty slot filled themselves first; else, now and then, an ask.
                        foreach (var p in colonists)
                            if (!WeaponKitSelf.Look(p, picks, now) && WeaponAsks.On)
                                WeaponAsks.Look(p, picks, now);
                    }
                    if (slice == 0 && Sched.FirstHourOfDay(now)) Prune(now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the weapon suggestions' pass", e);
                }
            }
        }

        /// <summary>Once a day: "not now" ended, a kit filled more than a day ago, and asks of colonists no longer here.</summary>
        private void Prune(int now)
        {
            foreach (var k in notNow.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList()) notNow.Remove(k);
            foreach (var k in fetched.Where(kv => now - kv.Value >= WeaponKitSelf.EachTicks || kv.Value > now).Select(kv => kv.Key).ToList()) fetched.Remove(k);
            var here = new HashSet<int>(Homes.FreeColonistsAnywhere().Select(p => p.thingIDNumber));
            asks.RemoveAll(a => !here.Contains(a.pawn) && now - a.tick > WeaponAskMath.SameWeaponDays * GenDate.TicksPerDay);
        }
    }

    /// <summary>A raid has come: the weapon suggestions note how many came, mechanoids, armour, a siege, a tribe.</summary>
    [HarmonyPatch(typeof(IncidentWorker_Raid), "PostProcessSpawnedPawns")]
    public static class Patch_WeaponRaidSeen
    {
        public static void Postfix(IncidentParms parms, List<Pawn> pawns)
        {
            try
            {
                WeaponPickBook.Instance?.Raided(parms, pawns);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the weapon suggestions noting a raid", e);
            }
        }
    }
}
