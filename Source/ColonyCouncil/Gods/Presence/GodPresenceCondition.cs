using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A god's own presence, as its condition's def declares it (Defs/GodDefs/Rimfolk_GodPresence_Own.xml; GodDef.presence):
    /// whose it is, what it does in the world (<see cref="PresenceEffect"/>, one kind each), its name when the colony is sure
    /// who it is ("{NAME} listening") and its words then; its name and words otherwise are the def's own label and
    /// description, what anyone can see ("the green leaning"). Its motes and sounds are named softly, the first loaded ones
    /// used (A RimWorld of Magic's, the game's), Rimfolk's own mote otherwise.
    /// </summary>
    public class OwnPresence : DefModExtension
    {
        /// <summary>The god's key ("Hollin").</summary>
        public string god;
        public PresenceEffect effect = PresenceEffect.None;
        /// <summary>Its name when the colony is sure whose it is, with {NAME} for the colony's name of the god.</summary>
        [MustTranslate] public string named;
        /// <summary>Its words then ({NAME}); the def's description is what is said when nobody knows.</summary>
        [MustTranslate] public string namedText;
        /// <summary>Never named, however sure the colony is (the Hunger: "the dark").</summary>
        public bool neverNamed;
        /// <summary>The motes it sends up over the land (mote things and flecks by name, the loaded ones used).</summary>
        public List<string> motes = new List<string>();
        /// <summary>The sounds it makes as it begins (SoundDefs by name, the loaded ones used).</summary>
        public List<string> sounds = new List<string>();

        [Unsaved] private List<Def> moteDefs;
        [Unsaved] private List<SoundDef> soundDefs;

        public List<Def> Motes => moteDefs ??= (motes ?? new List<string>())
            .Select(n => (Def)DefDatabase<FleckDef>.GetNamedSilentFail(n) ?? DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();

        public List<SoundDef> Sounds => soundDefs ??= (sounds ?? new List<string>()).Select(n => DefDatabase<SoundDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (god.NullOrEmpty()) yield return "a god's own presence names its god";
            if (!neverNamed && (named.NullOrEmpty() || !named.Contains("{NAME}"))) yield return "a god's own presence has its name for when the colony is sure, with {NAME}";
            if (!neverNamed && namedText.NullOrEmpty()) yield return "a god's own presence has its words for when the colony is sure";
        }
    }

    /// <summary>
    /// A god's presence on the sky (docs/design/gods.md, "Presence"; the game's eclipse is the pattern): for a few hours
    /// after a coming the sky takes the god's colour (dimmed for a wrath, never for a blessing) and the wind dies
    /// (GodPresencePatches.cs stills it). The shared one (Rimfolk_GodPresence) is named as the card names the god: plainly
    /// ("the presence of the Old Horn"), or, veiled, as what anyone can see ("a strange stillness"). A god's own
    /// (<see cref="OwnPresence"/>; round 2, docs/gods-next-proposals.md, section C) has its own names and words and does
    /// its own thing in the world (GodPresenceOwn.cs, GodPresenceShows.cs, GodPresenceOwnPatches.cs): its numbers are
    /// shown under its label, as the game shows any condition's.
    /// </summary>
    public class GameCondition_GodPresence : GameCondition
    {
        public const string DefName = GodPresenceOwnMath.SharedDef;

        public Color tint = Color.white;
        public float tintStrength = 0.5f;
        public float darken;
        public float wind = 0.15f;
        public string godName;
        public bool veiled;

        /// <summary>The god's key, and the kind of moment it came with (its strength and whether it is a wrath).</summary>
        public string godKey;
        public PresenceKind kind = PresenceKind.Appear;
        /// <summary>Its first look at the land done (the start of its show), and its one line said.</summary>
        public bool shown, said;
        /// <summary>The weather when it came (Cirrus holds it in its wrath), and the light Helios's long noon holds.</summary>
        public WeatherDef startWeather;
        public float heldGlow;
        /// <summary>Pip's prank: one door that won't open for one person, until someone else opens it or its hour is up.</summary>
        public Pawn prankPawn;
        public Building_Door prankDoor;
        public int prankUntil = -1;
        public bool prankSaid;
        /// <summary>Its once-a-presence moments: the dark's whisper, the dream told at dawn (the day's number).</summary>
        public bool whispered;
        public int dreamDay = -1;

        /// <summary>The last tick any presence lasts until (the wind's hook looks at nothing before then); kept by every presence begun or loaded.</summary>
        internal static int AnyUntil = -1;

        /// <summary>The last tick any god's own presence lasts until (the effects' hooks look at nothing after it).</summary>
        internal static int AnyOwnUntil = -1;

        [Unsaved] private IntVec3 focus = IntVec3.Invalid, dark = IntVec3.Invalid;

        /// <summary>Where Hollin's green leans (worked out once a presence, the land's plants being many).</summary>
        public IntVec3 FocusAt(Map map) => focus.IsValid ? focus : focus = GodPresenceShows.Focus(map);

        /// <summary>The corner the dark keeps (worked out once a presence).</summary>
        public IntVec3 DarkAt(Map map) => dark.IsValid ? dark : dark = GodPresenceShows.DarkCell(map);

        [Unsaved] private OwnPresence own;
        [Unsaved] private bool ownRead;

        /// <summary>Its own presence's declaration, or null for the shared one.</summary>
        public OwnPresence Own
        {
            get
            {
                if (!ownRead)
                {
                    own = def?.GetModExtension<OwnPresence>();
                    ownRead = true;
                }
                return own;
            }
        }

        public PresenceEffect Effect => Own?.effect ?? PresenceEffect.None;

        /// <summary>How strong its moment made it (GodPresenceOwnMath.Strength).</summary>
        public float Strength => GodPresenceOwnMath.Strength(kind);

        public bool Harsh => GodPresenceOwnMath.Harsh(kind);

        public Map Map => gameConditionManager?.ownerMap;

        public override int TransitionTicks => 400;

        /// <summary>How far in it is, 0 to 1, easing in and out over the transition.</summary>
        public float Lerp => GameConditionUtility.LerpInOutValue(this, TransitionTicks);

        [Unsaved] private string nameNow;
        [Unsaved] private int nameAt = -99999;

        /// <summary>
        /// The colony's name for the god when it is sure whose presence this is: named at its moment, or known since (a long
        /// stay: Tamiu among the graves, the Guest under the roof); null otherwise, and always for the Hunger's.
        /// </summary>
        public string NameNow
        {
            get
            {
                if (Own?.neverNamed ?? false) return null;
                if (!veiled && !godName.NullOrEmpty()) return godName;
                int now = Find.TickManager?.TicksGame ?? 0;
                if (now - nameAt < 250) return nameNow;
                nameAt = now;
                var g = godKey.NullOrEmpty() || Map == null ? null : Gods.ByKey(godKey);
                nameNow = g == null ? null : Gods.CardName(g, Map);
                return nameNow;
            }
        }

        /// <summary>Named only when the colony is sure whose it is (and never the Hunger's).</summary>
        public bool Named => NameNow != null;

        public override string Label
        {
            get
            {
                var o = Own;
                if (o == null)
                    return veiled || godName.NullOrEmpty() ? "CC_GodP_ConditionVeiled".Translate().Resolve() : "CC_GodP_Condition".Translate(godName.Named("NAME")).Resolve();
                string n = NameNow;
                return n != null && !o.named.NullOrEmpty() ? o.named.Formatted(n.Named("NAME")).Resolve() : def.label;
            }
        }

        public override string Description
        {
            get
            {
                var o = Own;
                if (o == null)
                    return veiled || godName.NullOrEmpty() ? "CC_GodP_ConditionVeiledDesc".Translate().Resolve() : "CC_GodP_ConditionDesc".Translate(godName.Named("NAME")).Resolve().CapitalizeFirst();
                string n = NameNow;
                string text = n != null && !o.namedText.NullOrEmpty() ? o.namedText.Formatted(n.Named("NAME")).Resolve() : def.description;
                var lines = GodPresenceOwn.NumberLines(this);
                return lines.Count == 0 ? text : text + "\n\n" + string.Join("\n", lines);
            }
        }

        public override void Init()
        {
            base.Init();
            Note();
        }

        private void Note()
        {
            int end = Permanent ? int.MaxValue : startTick + Duration;
            if (end > AnyUntil) AnyUntil = end;
            if (Effect != PresenceEffect.None && end > AnyOwnUntil) AnyOwnUntil = end;
        }

        /// <summary>Its end moved later (held while its god is on the land): the hooks keep looking until then.</summary>
        internal void Extended() => Note();

        public override float SkyTargetLerpFactor(Map map) => Lerp;

        public override SkyTarget? SkyTarget(Map map)
        {
            var e = Effect;
            bool harsh = Harsh;
            var c = GodPresenceMath.SkyColor(GodLookDef.Rgb(tint), tintStrength);
            var sky = new Color(c.r, c.g, c.b);
            var shadow = Color.Lerp(sky, Color.black, 0.25f);
            float dark = Mathf.Clamp01(darken + GodPresenceOwnMath.Darker(e, harsh));
            float sun = GenCelestial.CurCelestialSunGlow(map);
            float glow = sun * (1f - dark);
            // The long noon: the light of the hour it came, held. A moon too close: the night as light as her face is full.
            glow = Mathf.Max(glow, GodPresenceOwnMath.HeldGlow(e, heldGlow));
            if (e == PresenceEffect.MoonSleep && sun < 0.4f) glow = Mathf.Max(glow, GodPresenceOwnMath.NightGlow(e, harsh, GodsOnMapMath.MoonLight(GodOmens.MoonTonight)));
            glow = Mathf.Clamp01(glow + GodPresenceOwnMath.Brighter(e, harsh));
            return new SkyTarget(glow, new SkyColorSet(sky, shadow, sky, 1f - dark * 0.5f), 1f, 1f - dark);
        }

        public override float TemperatureOffset() => GodPresenceOwnMath.Temperature(Effect, Strength, Harsh) * GameConditionUtility.LerpInOutValue(this, 2500f);

        [Unsaved] private WeatherDef forced;
        [Unsaved] private int forcedAt = -99999;

        /// <summary>The weather it brings or holds (GodPresenceOwnMath.Weather), worked out again every 250 ticks (the game asks often).</summary>
        public override WeatherDef ForcedWeather()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now - forcedAt < 250 && now >= forcedAt) return forced;
            forcedAt = now;
            forced = WorkOutWeather();
            return forced;
        }

        private WeatherDef WorkOutWeather()
        {
            var e = Effect;
            var map = Map;
            if (e == PresenceEffect.None || map == null) return null;
            try
            {
                var names = GodPresenceOwnMath.Weather(e, Harsh, SeasonIndex(map), map.mapTemperature.OutdoorTemp, TicksPassed / 2500f, startWeather?.defName);
                foreach (var n in names)
                {
                    var w = DefDatabase<WeatherDef>.GetNamedSilentFail(n);
                    if (w != null) return w;
                }
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("gods: a presence's weather", ex);
            }
            return null;
        }

        /// <summary>The season as 0 spring to 3 winter (the game's Season enum starts with Undefined).</summary>
        private static int SeasonIndex(Map map)
        {
            switch (GenLocalDate.Season(map))
            {
                case Season.Spring: return 0;
                case Season.Summer:
                case Season.PermanentSummer:
                    return 1;
                case Season.Fall: return 2;
                default: return 3;
            }
        }

        public override float SkyGazeChanceFactor(Map map) => base.SkyGazeChanceFactor(map) * GodPresenceOwnMath.GazeChance(Effect, Harsh);

        public override float SkyGazeJoyGainFactor(Map map) => base.SkyGazeJoyGainFactor(map) * GodPresenceOwnMath.GazeJoy(Effect, Harsh);

        public override void GameConditionTick()
        {
            base.GameConditionTick();
            if (Effect == PresenceEffect.None) return;
            var map = Map;
            if (map == null) return;
            try
            {
                GodPresenceShows.Tick(this, map, Find.TickManager.TicksGame);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's own presence", e);
            }
        }

        public override void End()
        {
            var map = Map;
            // As it was while it lasted: whether the colony was sure whose it was, its label, a wrath or not.
            bool named = Effect != PresenceEffect.None && Named, harsh = Harsh;
            string label = Effect != PresenceEffect.None ? Label : null;
            base.End();
            if (Effect == PresenceEffect.None || map == null) return;
            try
            {
                GodPresenceShows.Ended(this, map);
                // Each one's own belief, from a presence lived through (Gods/Belief/PawnGods.cs).
                PawnGods.PresencePassed(GodPresenceOwn.GodOf(this), map, named, harsh, label);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a god's own presence ending", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tint, "tint", Color.white);
            Scribe_Values.Look(ref tintStrength, "tintStrength", 0.5f);
            Scribe_Values.Look(ref darken, "darken");
            Scribe_Values.Look(ref wind, "wind", 0.15f);
            Scribe_Values.Look(ref godName, "godName");
            Scribe_Values.Look(ref veiled, "veiled");
            Scribe_Values.Look(ref godKey, "godKey");
            Scribe_Values.Look(ref kind, "kind", PresenceKind.Appear);
            Scribe_Values.Look(ref shown, "shown");
            Scribe_Values.Look(ref said, "said");
            Scribe_Defs.Look(ref startWeather, "startWeather");
            Scribe_Values.Look(ref heldGlow, "heldGlow");
            Scribe_References.Look(ref prankPawn, "prankPawn");
            Scribe_References.Look(ref prankDoor, "prankDoor");
            Scribe_Values.Look(ref prankUntil, "prankUntil", -1);
            Scribe_Values.Look(ref prankSaid, "prankSaid");
            Scribe_Values.Look(ref whispered, "whispered");
            Scribe_Values.Look(ref dreamDay, "dreamDay", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Note();
                GodPresenceOwn.Loaded(this);
            }
        }

        /// <summary>
        /// Begins a presence on <paramref name="map"/>: the sky in the god's colour and the wind dying, for the kind's hours;
        /// the god's own (<paramref name="presence"/>, its condition's def) where it has one, doing its own thing in the
        /// world and lasting as its nature asks; the shared one otherwise.
        /// </summary>
        internal static void Begin(Map map, PresenceKind kind, Color color, float still, string name, bool veiled, GameConditionDef presence = null, string godKey = null)
        {
            var def = presence ?? DefDatabase<GameConditionDef>.GetNamedSilentFail(DefName);
            var effect = def?.GetModExtension<OwnPresence>()?.effect ?? PresenceEffect.None;
            int ticks = effect != PresenceEffect.None && map != null ? GodPresenceOwnMath.Ticks(effect, kind, GenLocalDate.HourOfDay(map)) : GodPresenceMath.ConditionTicks(kind);
            if (map == null || ticks <= 0 || def == null) return;
            try
            {
                // One presence a map: a new coming takes the place of the last.
                var old = map.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>();
                old?.End();
                var cond = (GameCondition_GodPresence)GameConditionMaker.MakeCondition(def, ticks);
                cond.tint = color;
                cond.tintStrength = GodPresenceMath.SkyTint(kind);
                cond.darken = GodPresenceMath.Darken(kind);
                cond.wind = GodPresenceMath.WindFactor(still, kind);
                cond.godName = name;
                cond.veiled = veiled;
                cond.godKey = godKey;
                cond.kind = kind;
                map.gameConditionManager.RegisterCondition(cond);
                GodPresenceOwn.Forget(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the presence on the sky", e);
            }
        }

        /// <summary>The wind's factor on a map now (1: as it is).</summary>
        internal static float WindOn(Map map)
        {
            if (map == null || Find.TickManager.TicksGame > AnyUntil) return 1f;
            var c = map.gameConditionManager.GetActiveCondition<GameCondition_GodPresence>();
            if (c == null) return 1f;
            float t = GameConditionUtility.LerpInOutValue(c, c.TransitionTicks);
            return Mathf.Lerp(1f, c.wind, t);
        }
    }
}
