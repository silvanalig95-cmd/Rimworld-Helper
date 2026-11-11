using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A god's presence on the sky (docs/design/gods.md, "Presence"; the game's eclipse is the pattern): for a few hours
    /// after a coming the sky takes the god's colour (dimmed for a wrath, never for a blessing) and the wind dies
    /// (GodPresencePatches.cs stills it). Named as the card names the god: plainly ("the presence of the Storm-father"),
    /// or, veiled, as what anyone can see ("a strange stillness").
    /// </summary>
    public class GameCondition_GodPresence : GameCondition
    {
        public const string DefName = "Rimfolk_GodPresence";

        public Color tint = Color.white;
        public float tintStrength = 0.5f;
        public float darken;
        public float wind = 0.15f;
        public string godName;
        public bool veiled;

        /// <summary>The last tick any presence lasts until (the wind's hook looks at nothing before then); kept by every presence begun or loaded.</summary>
        internal static int AnyUntil = -1;

        public override int TransitionTicks => 400;

        public override string Label =>
            veiled || godName.NullOrEmpty() ? "CC_GodP_ConditionVeiled".Translate().Resolve() : "CC_GodP_Condition".Translate(godName.Named("NAME")).Resolve();

        public override string Description =>
            veiled || godName.NullOrEmpty() ? "CC_GodP_ConditionVeiledDesc".Translate().Resolve() : "CC_GodP_ConditionDesc".Translate(godName.Named("NAME")).Resolve().CapitalizeFirst();

        public override void Init()
        {
            base.Init();
            Note();
        }

        private void Note()
        {
            int end = Permanent ? int.MaxValue : startTick + Duration;
            if (end > AnyUntil) AnyUntil = end;
        }

        public override float SkyTargetLerpFactor(Map map) => GameConditionUtility.LerpInOutValue(this, TransitionTicks);

        public override SkyTarget? SkyTarget(Map map)
        {
            var c = GodPresenceMath.SkyColor(GodLookDef.Rgb(tint), tintStrength);
            var sky = new Color(c.r, c.g, c.b);
            var shadow = Color.Lerp(sky, Color.black, 0.25f);
            float glow = GenCelestial.CurCelestialSunGlow(map) * (1f - darken);
            return new SkyTarget(glow, new SkyColorSet(sky, shadow, sky, 1f - darken * 0.5f), 1f, 1f - darken);
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
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Note();
        }

        /// <summary>Begins a presence on <paramref name="map"/>: the sky in the god's colour and the wind dying, for the kind's hours.</summary>
        internal static void Begin(Map map, PresenceKind kind, Color color, float still, string name, bool veiled)
        {
            int ticks = GodPresenceMath.ConditionTicks(kind);
            var def = DefDatabase<GameConditionDef>.GetNamedSilentFail(DefName);
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
                map.gameConditionManager.RegisterCondition(cond);
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
