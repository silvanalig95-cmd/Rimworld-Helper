using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Each god's own presence (round 2 of docs/gods-next-proposals.md, section C; the player, 14 November 2026: "gods effect
    /// should be noticeable"; "each of the gods should feel really unique in what it does and whether or how it interacts
    /// with the world"). A god's def names its presence (<c>GodDef.presence</c>: a GameConditionDef with an
    /// <see cref="OwnPresence"/>); a god whose def names none keeps the shared one (the sky and the stilled wind), as Mog
    /// does until her remake brings hers. Here: which presence is on a map now (cheaply, for the hooks), the numbers under
    /// its label, whether its god is on the land (the Old Horn grazing, the Guest under the roof hold theirs), a presence
    /// begun with no moment (the Guest let in), and its hour. What each one shows is GodPresenceShows.cs; the game's hooks
    /// are GodPresenceOwnPatches.cs; the rules and sizes GodPresenceOwnMath.
    /// </summary>
    public static class GodPresenceOwn
    {
        /// <summary>A god's own presence, as its def declares it; null for the shared one.</summary>
        public static GameConditionDef DefOf(God g) => g?.def?.presence;

        private static readonly Dictionary<int, (int tick, GameCondition_GodPresence c)> cache = new Dictionary<int, (int, GameCondition_GodPresence)>();
        private static Game cacheGame;

        /// <summary>A god's own presence on the map now (one with an effect), or null: nothing at all to look up when none lasts anywhere.</summary>
        public static GameCondition_GodPresence On(Map map)
        {
            if (map == null || Current.Game == null) return null;
            int now = Find.TickManager.TicksGame;
            if (now > GameCondition_GodPresence.AnyOwnUntil) return null;
            if (cacheGame != Current.Game)
            {
                cache.Clear();
                cacheGame = Current.Game;
            }
            if (cache.TryGetValue(map.uniqueID, out var hit) && hit.tick == now) return hit.c;
            var c = map.gameConditionManager?.GetActiveCondition<GameCondition_GodPresence>();
            if (c != null && c.Effect == PresenceEffect.None) c = null;
            cache[map.uniqueID] = (now, c);
            return c;
        }

        /// <summary>The effect on the map now, its strength and whether it is a wrath (None when there is none).</summary>
        public static PresenceEffect EffectOn(Map map, out float strength, out bool harsh)
        {
            var c = On(map);
            strength = c?.Strength ?? 0f;
            harsh = c?.Harsh ?? false;
            return c?.Effect ?? PresenceEffect.None;
        }

        /// <summary>The cache let go (a presence begun or ended this tick).</summary>
        internal static void Forget(Map map)
        {
            if (map != null) cache.Remove(map.uniqueID);
        }

        /// <summary>A presence loaded with the game: nothing kept between games.</summary>
        internal static void Loaded(GameCondition_GodPresence c)
        {
            cache.Clear();
            GodPresenceShows.PrankLoaded(c);
        }

        // ================= The numbers under its label =================

        private static string Pct(float f) => Mathf.Abs(f).ToStringPercent();

        private static string Deg(float d) => Mathf.Abs(d).ToStringTemperatureOffset("F0");

        /// <summary>What it changes in numbers, in plain words under its label (as the game shows any condition's): nothing a god wants or thinks.</summary>
        public static List<string> NumberLines(GameCondition_GodPresence c)
        {
            var lines = new List<string>();
            if (c == null) return lines;
            var e = c.Effect;
            float s = c.Strength;
            bool h = c.Harsh;
            void Add(string key, params NamedArgument[] args) => lines.Add(key.Translate(args).Resolve());
            float t = GodPresenceOwnMath.Temperature(e, s, h);
            if (t >= 0.5f) Add(e == PresenceEffect.WarmStone ? "CC_GodPO_Num_WarmerRock" : "CC_GodPO_Num_Warmer", Deg(t).Named("DEG"));
            else if (t <= -0.5f) Add("CC_GodPO_Num_Colder", Deg(t).Named("DEG"));
            switch (e)
            {
                case PresenceEffect.Growth:
                {
                    float g = GodPresenceOwnMath.Growth(e, s, h, PresencePlant.Wild, false);
                    if (g > 1.001f) Add("CC_GodPO_Num_Growth", Pct(g - 1f).Named("PCT"));
                    break;
                }
                case PresenceEffect.WarmStone:
                {
                    float m = GodPresenceOwnMath.MiningYield(e, s, h);
                    if (m > 1.001f) Add("CC_GodPO_Num_Mining", Pct(m - 1f).Named("PCT"));
                    break;
                }
                case PresenceEffect.Grazing:
                    if (GodPresenceOwnMath.CalmsBeasts(e, h)) Add("CC_GodPO_Num_Beasts");
                    if (GodPresenceOwnMath.Insults(e, s, h) < 0.999f)
                        Add("CC_GodPO_Num_Peace", Pct(1f - GodPresenceOwnMath.Insults(e, s, h)).Named("PCT"), Pct(1f - GodPresenceOwnMath.Fights(e, s, h)).Named("FIGHTS"));
                    break;
                case PresenceEffect.Rot:
                    Add("CC_GodPO_Num_Rot", Pct(GodPresenceOwnMath.Rot(e, s, h, false) - 1f).Named("PCT"));
                    Add("CC_GodPO_Num_Fungus", Pct(GodPresenceOwnMath.Growth(e, s, h, PresencePlant.Fungus, false) - 1f).Named("PCT"));
                    if (GodPresenceOwnMath.Immunity(e, s, h) > 1.001f) Add("CC_GodPO_Num_Immunity", Pct(GodPresenceOwnMath.Immunity(e, s, h) - 1f).Named("PCT"));
                    break;
                case PresenceEffect.Warmth:
                    if (GodPresenceOwnMath.Insults(e, s, h) < 0.999f) Add("CC_GodPO_Num_Kinder", Pct(1f - GodPresenceOwnMath.Insults(e, s, h)).Named("PCT"));
                    if (GodPresenceOwnMath.HearthKept(e, s, h) > 0f) Add("CC_GodPO_Num_Hearths");
                    break;
                case PresenceEffect.Hum:
                    if (GodPresenceOwnMath.BatteryKept(e, s, h) > 0f) Add("CC_GodPO_Num_Batteries", Pct(GodPresenceOwnMath.BatteryKept(e, s, h)).Named("PCT"));
                    if (GodPresenceOwnMath.BenchSpeed(e, s, h) > 1.001f) Add("CC_GodPO_Num_Benches", Pct(GodPresenceOwnMath.BenchSpeed(e, s, h) - 1f).Named("PCT"));
                    break;
                case PresenceEffect.HighSky:
                    Add("CC_GodPO_Num_WeatherHolds");
                    if (GodPresenceOwnMath.GazeJoy(e, h) > 1.001f) Add("CC_GodPO_Num_Gaze", Pct(GodPresenceOwnMath.GazeJoy(e, h) - 1f).Named("PCT"));
                    break;
                case PresenceEffect.WhiteSilence:
                    Add("CC_GodPO_Num_Silence");
                    Add("CC_GodPO_Num_NoRot");
                    Add("CC_GodPO_Num_ColdSooner", Deg(GodPresenceOwnMath.ColdSooner(e, s, h, true)).Named("DEG"));
                    break;
                case PresenceEffect.Dark:
                    Add("CC_GodPO_Num_Lamps", Pct(1f - GodPresenceOwnMath.Lamps(e, s, h)).Named("PCT"));
                    Add("CC_GodPO_Num_Psychic", Pct(GodPresenceOwnMath.Psychic(e, s, h)).Named("PCT"));
                    break;
                case PresenceEffect.LongNoon:
                    if (c.heldGlow >= 0.5f) Add("CC_GodPO_Num_LightHeld");
                    Add("CC_GodPO_Num_Solar", Pct(GodPresenceOwnMath.Solar(e, s) - 1f).Named("PCT"));
                    if (GodPresenceOwnMath.Growth(e, s, h, PresencePlant.Crop, false) > 1.001f)
                        Add("CC_GodPO_Num_Crops", Pct(GodPresenceOwnMath.Growth(e, s, h, PresencePlant.Crop, false) - 1f).Named("PCT"));
                    break;
                case PresenceEffect.MoonSleep:
                    if (!h)
                    {
                        Add("CC_GodPO_Num_MoonNight");
                        Add("CC_GodPO_Num_SleepHeal", Pct(GodPresenceOwnMath.SleepHealing(e, s, h, true) - 1f).Named("PCT"));
                    }
                    break;
                case PresenceEffect.Rain:
                    Add("CC_GodPO_Num_Rain");
                    if (GodPresenceOwnMath.Growth(e, s, h, PresencePlant.Wild, true) > 1.001f)
                        Add("CC_GodPO_Num_Wet", Pct(GodPresenceOwnMath.Growth(e, s, h, PresencePlant.Wild, true) - 1f).Named("PCT"));
                    if (ModsConfig.IsActive(DubsWells)) Add("CC_GodPO_Num_Wells");
                    break;
                case PresenceEffect.Glint:
                {
                    float tr = GodPresenceOwnMath.Trade(e, s, h);
                    if (tr > 0f) Add("CC_GodPO_Num_TradeBetter", Pct(tr).Named("PCT"));
                    else if (tr < 0f) Add("CC_GodPO_Num_TradeWorse", Pct(tr).Named("PCT"));
                    break;
                }
            }
            return lines;
        }

        /// <summary>Dubs Bad Hygiene, whose wells and sicknesses from bad water Murmur's presence keeps clean.</summary>
        public const string DubsWells = "dubwise.dubsbadhygiene";

        // ================= Its god on the land =================

        /// <summary>The god whose presence it is (by its key), or null.</summary>
        public static God GodOf(GameCondition_GodPresence c) => c == null || c.godKey.NullOrEmpty() ? null : Gods.ByKey(c.godKey);

        /// <summary>The god walks this land now: as the sacred beast, the stranger staying, the woken sleeper or the risen shape.</summary>
        public static Pawn Here(God g, Map map)
        {
            var b = GodsOnMapBook.Instance;
            if (g == null || map == null || b == null) return null;
            bool On(Pawn p) => p != null && p.Spawned && p.Map == map && !p.Dead;
            if (b.beast != null && b.beast.god == g.id && On(b.beast.a)) return b.beast.a;
            if (b.stranger != null && b.stranger.god == g.id && On(b.stranger.x) && b.stranger.phase == StrangerPhase.Staying) return b.stranger.x;
            if (b.woken != null && b.wokenGod == g.id && On(b.woken)) return b.woken;
            return null;
        }

        /// <summary>
        /// A god's own presence begun with no moment of its own, when its god comes onto the land quietly (the Guest let in
        /// at the gate): named as the colony knows it, which for a god in disguise is not at all ("a warm evening").
        /// </summary>
        public static void Quiet(God g, Map map)
        {
            var def = DefOf(g);
            var own = def?.GetModExtension<OwnPresence>();
            if (g == null || map == null || own == null || !Gods.On || !GodPresenceOwnMath.HeldWhileHere(own.effect)) return;
            // Never in place of another presence on the land (its own, or another god's moment): it begins on the hour after that has passed.
            if (map.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>() != null) return;
            string known = Gods.CardName(g, map);
            var look = GodsWiring.LookOf(g);
            GameCondition_GodPresence.Begin(map, PresenceKind.Appear, g.colour, look?.still ?? 0.5f, known ?? GodLore.LabelFor(Gods.Name(g)), known == null, def, g.Key);
            Forget(map);
        }

        // ================= Its hour =================

        /// <summary>The gods' hour (GodsOnMapBook.Hourly): each home map's own presence has its hour (a croak, a call, a dream at dawn).</summary>
        internal static void Hourly(int now)
        {
            foreach (var map in Homes.All.ToList())
            {
                var c = On(map);
                if (c == null)
                {
                    // A god whose presence lasts while it walks the land, still here when another's has passed: its own again.
                    if (map.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>() == null)
                        foreach (var g in Gods.All)
                        {
                            var own = DefOf(g)?.GetModExtension<OwnPresence>();
                            if (own == null || !GodPresenceOwnMath.HeldWhileHere(own.effect) || Here(g, map) == null) continue;
                            Quiet(g, map);
                            break;
                        }
                    continue;
                }
                try
                {
                    GodPresenceShows.Hourly(c, map, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a god's own presence, its hour", e);
                }
            }
        }

        // ================= Who says what =================

        public const string SayPool = "Rimfolk_Pool_GodPO_Say";
        public const string DreamPool = "Rimfolk_Pool_GodPO_Dream";

        /// <summary>
        /// One of the colony's says one sentence of what they see of a presence (its tag: the moment, "notice:growth"), with
        /// the god's name only where the colony is sure whose it is. False when nothing fits or nobody is there to say it.
        /// </summary>
        internal static bool Say(Pawn p, GameCondition_GodPresence c, string tag)
        {
            string line = Line(SayPool, p, c, tag);
            return !line.NullOrEmpty() && Speech.Say(p, line);
        }

        /// <summary>A line from one of the presence's pools for <paramref name="p"/> (tagged as <see cref="Say"/>), with {PAWN} and {GOD} filled; null when none fits.</summary>
        internal static string Line(string pool, Pawn p, GameCondition_GodPresence c, string tag, params NamedArgument[] more)
        {
            if (p == null || !p.Spawned || !GodOmens.Awake(p)) return null;
            var tags = new List<string> { tag };
            if (c != null && c.Named) tags.Add("named");
            if (c != null && c.Harsh) tags.Add("wrath");
            if (Worship.Scoffer(p)) tags.Add("scoffer");
            string line = Flavor.FromPool(pool, p, null, null, tags.ToArray());
            if (line.NullOrEmpty()) return null;
            var args = new List<NamedArgument> { p.Named("PAWN"), (c?.godName ?? "").Named("GOD") };
            if (more != null) args.AddRange(more);
            return line.Formatted(args.ToArray()).Resolve().CapitalizeFirst();
        }
    }
}
