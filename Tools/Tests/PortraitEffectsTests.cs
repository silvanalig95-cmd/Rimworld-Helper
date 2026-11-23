using ColonyCouncil;

/// <summary>
/// What a person goes through, laid over their portraits (UI/Faces/PortraitEffectsMath.cs, PortraitEffects.cs; the player,
/// 23 November 2026: "effects sound great, go ahead now! should be toggleable in case of performance issues"); and the
/// library read again when a game starts, with the shortcuts to Rimfolk's settings ("the reread on startup and new game,
/// or manual reload is enough").
/// </summary>
public static class PortraitEffectsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "portrait effects: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";

        // ---------------- What each condition shows ----------------
        {
            var calm = PortraitEffectsMath.Levels(PortraitCondition.Calm);
            C(!calm.Any, "someone whole, rested and at ease shows nothing (and draws nothing more)");
            PortraitEffectLevels With(Func<PortraitCondition, PortraitCondition> f) => PortraitEffectsMath.Levels(f(PortraitCondition.Calm));

            var trickle = With(c => { c.bleed = 0.05f; return c; });
            var gush = With(c => { c.bleed = 1.5f; c.bloodLoss = 0.5f; return c; });
            var lostOnly = With(c => { c.bloodLoss = 0.4f; return c; });
            C(trickle.bleed > 0.29f && trickle.bleed < 0.35f && gush.bleed > 0.95f && lostOnly.bleed > 0.2f && lostOnly.bleed < trickle.bleed
              && With(c => { c.bleed = 0.01f; return c; }).bleed == 0f,
                $"bleeding: a small cut's trickle a little ({trickle.bleed:0.00}), a bad wound with blood lost at full ({gush.bleed:0.00}), blood already lost without bleeding less ({lostOnly.bleed:0.00}), a scratch nothing");
            var pain = With(c => { c.pain = 0.6f; return c; });
            var weak = With(c => { c.health = 0.4f; return c; });
            C(pain.hurt > 0.7f && weak.hurt > 0.75f && With(c => { c.pain = 0.1f; c.health = 0.9f; return c; }).hurt == 0f,
                "badly hurt by pain or by health, a little pain or a scratch nothing");
            var low = With(c => { c.mood = 0.2f; return c; });
            var atMinor = With(c => { c.mood = 0.35f; return c; });
            var happy = With(c => { c.mood = 0.6f; return c; });
            var broke = With(c => { c.breaking = true; return c; });
            C(happy.stress == 0f && atMinor.stress > 0.1f && atMinor.stress < 0.2f && low.stress > 0.45f && low.stress < 0.65f && broke.stress == 1f && broke.breaking == 1f,
                $"near a break: nothing in a good mood, a little at the minor threshold ({atMinor.stress:0.00}), more toward the extreme one ({low.stress:0.00}), all of it and the harsher tint in a break");
            var flu = With(c => { c.sick = 0.5f; return c; });
            C(flu.sick > 0.6f && flu.sick < 0.65f && With(c => { c.sick = 1f; return c; }).sick == 1f, "sick or infected: from a quarter up as the illness nears its deadly end");
            var shiver = With(c => { c.cold = 0.04f; return c; });
            var freezing = With(c => { c.cold = 0.62f; c.heat = 0.35f; return c; });
            C(shiver.cold == 0f && freezing.cold == 1f && freezing.heat > 0.5f && freezing.heat < 0.55f, "hypothermia and heatstroke from the game's first stage to its extreme one");
            var tired = With(c => { c.rest = 0.14f; return c; });
            var sleeping = With(c => { c.rest = 0.05f; c.asleep = true; return c; });
            C(tired.tired > 0.45f && tired.tired < 0.55f && sleeping.tired == 0f && With(c => { c.rest = 0.5f; return c; }).tired == 0f,
                "exhausted from the game's tired down; never while asleep");
            var tipsy = With(c => { c.drunk = 0.35f; return c; });
            var flake = With(c => { c.high = true; return c; });
            C(tipsy.high == 0.5f && flake.high == 0.5f && With(c => { c.drunk = 0.9f; return c; }).high == 1f, "drunk by alcohol's severity, another drug's high half way");
            var starving = With(c => { c.hunger = 0.8f; return c; });
            C(starving.hunger == 1f && With(c => { c.hunger = 0.03f; return c; }).hunger == 0f, "starving by malnutrition, from its first stage");
            var out_ = With(c => { c.unconscious = true; c.rest = 0f; return c; });
            C(out_.dim == 1f && out_.tired == 0f, "unconscious: dimmed, never also exhausted");
            var dead = With(c => { c.dead = true; c.bleed = 2f; c.unconscious = true; return c; });
            C(!dead.Any, "the dead show none of it (their picture is grey)");
        }

        // ---------------- Coming and going softly, the pulse, the shake ----------------
        {
            var none = new PortraitEffectLevels();
            var want = new PortraitEffectLevels { bleed = 1f, cold = 0.5f };
            var a = PortraitEffectsMath.Toward(none, want, 0.4f);
            var b = PortraitEffectsMath.Toward(a, want, 2f);
            var back = PortraitEffectsMath.Toward(b, none, 0.25f);
            C(a.bleed == 0.4f && a.cold == 0.4f && b.bleed == 1f && b.cold == 0.5f && back.bleed == 0.75f && back.cold == 0.25f,
                $"an effect comes and goes at {PortraitEffectsMath.FadePerSecond} a second, never past what is wanted");
            float lo = 9f, hi = -9f, slow = 0f, fast = 0f;
            for (int i = 0; i < 400; i++)
            {
                float t = i * 0.01f, p = PortraitEffectsMath.Pulse(t, 1f);
                lo = Math.Min(lo, p);
                hi = Math.Max(hi, p);
            }
            for (int i = 1; i < 1000; i++)
            {
                if (PortraitEffectsMath.Pulse(i * 0.01f, 0f) > PortraitEffectsMath.Pulse((i - 1) * 0.01f, 0f) != PortraitEffectsMath.Pulse((i + 1) * 0.01f, 0f) > PortraitEffectsMath.Pulse(i * 0.01f, 0f)) slow++;
                if (PortraitEffectsMath.Pulse(i * 0.01f, 1f) > PortraitEffectsMath.Pulse((i - 1) * 0.01f, 1f) != PortraitEffectsMath.Pulse((i + 1) * 0.01f, 1f) > PortraitEffectsMath.Pulse(i * 0.01f, 1f)) fast++;
            }
            C(lo >= 0.699f && hi <= 1.001f && fast > slow, $"the bleeding pulses between 0.7 and 1, faster as it bleeds more ({slow / 2} beats in 10 seconds at a trickle, {fast / 2} at a gush)");
            float most = 0f, mostX = 0f;
            for (int i = 0; i < 2000; i++)
            {
                PortraitEffectsMath.Shake(new PortraitEffectLevels { stress = 1f, high = 1f }, i * 0.013f, 77, out float lean, out float dx);
                most = Math.Max(most, Math.Abs(lean));
                mostX = Math.Max(mostX, Math.Abs(dx));
            }
            PortraitEffectsMath.Shake(new PortraitEffectLevels { stress = 0.45f }, 3.7f, 77, out float calmLean, out float calmDx);
            C(most > 0.5f && most < 2.5f && mostX < 0.01f && calmLean == 0f && calmDx == 0f,
                $"the tremble from halfway to a break and the drunk's wobble: at most {most:0.00} degrees and {mostX * 100f:0.00}% of the width; below halfway still");
        }

        // ---------------- The textures' shapes ----------------
        {
            C(PortraitEffectsMath.Edge(0.5f, 0.5f, false) == 0f && PortraitEffectsMath.Edge(0.02f, 0.02f, false) > 0.9f && PortraitEffectsMath.Edge(0.5f, 0.02f, false) > 0.6f
              && PortraitEffectsMath.Edge(0.5f, 0.02f, false) < PortraitEffectsMath.Edge(0.02f, 0.02f, false),
                "an edge: clear in the middle, strong at the edges, strongest in the corners");
            C(PortraitEffectsMath.Edge(0.02f, 0.02f, true) == 0f && PortraitEffectsMath.Edge(0.5f, 0.02f, true) > 0.9f && PortraitEffectsMath.Inside(0.02f, 0.02f, true, 128) == 0f
              && PortraitEffectsMath.Inside(0.5f, 0.5f, true, 128) == 1f, "a round portrait's edge at its circle, nothing outside it");
            C(PortraitEffectsMath.Rim(0.5f, 0.5f, false) == 0f && PortraitEffectsMath.Rim(0.3f, 0.3f, false) == 0f && PortraitEffectsMath.Rim(0.995f, 0.5f, false) > 0.9f,
                "the red rim: only along the edges");
            C(PortraitEffectsMath.Bands(0.5f, 0.5f, false) == 0f && PortraitEffectsMath.Bands(0.5f, 0.02f, false) > 0.9f && PortraitEffectsMath.Bands(0.02f, 0.5f, false) == 0f,
                "the tired bands: at the top and the foot, not at the sides");
            float corner = 0f, middle = 0f;
            for (int i = 0; i < 10; i++)
            {
                corner += PortraitEffectsMath.Frost(0.02f + i * 0.004f, 0.03f, false, 17);
                middle += PortraitEffectsMath.Frost(0.45f + i * 0.01f, 0.5f, false, 17);
            }
            C(corner / 10f > 0.5f && middle == 0f && PortraitEffectsMath.Frost(0.02f, 0.02f, true, 17) == 0f,
                $"frost thick in the corners ({corner / 10f:0.00}), none in the middle, none outside a round portrait");
            float nlo = 1f, nhi = 0f;
            for (int i = 0; i < 500; i++)
            {
                float v = PortraitEffectsMath.Noise(i * 0.37f, i * 0.53f, 5);
                nlo = Math.Min(nlo, v);
                nhi = Math.Max(nhi, v);
            }
            C(nlo >= 0f && nhi <= 1f && nhi - nlo > 0.6f && PortraitEffectsMath.Noise(3.2f, 1.7f, 5) == PortraitEffectsMath.Noise(3.2f, 1.7f, 5), "the frost's grain: 0 to 1, varied, the same each time");
        }

        // ---------------- The setting, the game's side, the seams ----------------
        string fx = Read("UI/Faces/PortraitEffects.cs"), settings = Read("CouncilSettings.cs"), catalog = Read("UI/SettingsCatalog.cs");
        string inspect = Read("UI/Faces/InspectPortrait.cs"), card = Read("UI/ColonistCard/ColonistCardWindow.cs"), kit = Read("UI/UiKit.cs"), stage = Read("UI/Faces/FaceEngineStage.cs"), engine = Read("UI/Faces/FaceEngine.cs");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Portraits.xml"));
        C(settings.Contains("public PortraitEffectsMode portraitEffects = PortraitEffectsMode.Everywhere;") && settings.Contains("Scribe_Values.Look(ref portraitEffects, \"portraitEffects\", PortraitEffectsMode.Everywhere);")
          && catalog.Contains("Choice(\"portraitEffects\", \"CC_SetLabel_PortraitEffects\"") && catalog.Contains("new[] { \"Off\", \"Large\", \"Everywhere\" }"),
            "a setting: off, the large portraits, or every portrait (on everywhere at first), saved");
        var words = new[] { "CC_SetLabel_PortraitEffects", "CC_SetTip_PortraitEffects", "CC_PortraitEffects_Off", "CC_PortraitEffects_Large", "CC_PortraitEffects_Everywhere",
            "CC_SetNote_PortraitEffects_Off", "CC_SetNote_PortraitEffects_Large", "CC_SetNote_PortraitEffects_Everywhere" };
        C(words.All(w => keyed.Contains("<" + w + ">")), "its words in the language file");
        C(fx.Contains("return m == PortraitEffectsMode.Everywhere || m == PortraitEffectsMode.Large && large;") && fx.Contains("if (round && r.width < PortraitEffectsMath.RoundFrom) return;")
          && fx.Contains("if (!e.Any) return;") && fx.Contains("private const float ReadEvery = 0.5f;") && fx.Contains("if (now - e.readAt >= ReadEvery)"),
            "light on the game: off draws nothing, a small round portrait none, someone at ease nothing more, the game read twice a second at most");
        C(fx.Contains("c.bleed = hs.BleedRateTotal;") && fx.Contains("d == HediffDefOf.BloodLoss") && fx.Contains("d == HediffDefOf.Hypothermia") && fx.Contains("d == HediffDefOf.Heatstroke")
          && fx.Contains("d == HediffDefOf.Malnutrition") && fx.Contains("h is Hediff_Alcohol") && fx.Contains("h is Hediff_High") && fx.Contains("d.makesSickThought || d == HediffDefOf.WoundInfection")
          && fx.Contains("!health.capacities.CanBeAwake") && fx.Contains("breaker.BreakThresholdMinor") && fx.Contains("c.breaking = p.InMentalState;") && fx.Contains("p.needs.rest.CurLevel"),
            "read from the game's own health, needs and mind (any mod's illness the game counts as sickness, any drug's high)");
        C(fx.Contains("new Texture2D(size, size, TextureFormat.RGBA32, false)") && fx.Contains("textures[key] = tex;") && !fx.Contains("new Color("),
            "each effect one small texture painted once, its colour in its pixels (no colour of the interface's own)");
        C(inspect.Contains("PortraitEffects.Draw(inner, p, true, false);") && card.Contains("PortraitEffects.Draw(tile.ContractedBy(3f), pawn, true, false);")
          && kit.Contains("PortraitEffects.Draw(inner, p, false, true, alpha);"), "laid over the large portrait, the colonist card's, and the round portraits");
        C(stage.Contains("PortraitEffects.Shake(p, out float shakeLean, out float shakeDx);") && stage.Contains("dx += shakeX;") && engine.Contains("PortraitEffects.Shake(p, out float shakeLean, out _);"),
            "the tremble and the wobble move the person in a moving portrait");

        // ---------------- The library read again when a game starts; the shortcuts ----------------
        string folder = Read("UI/Faces/PortraitFolder.cs"), keeper = Read("UI/Faces/PortraitKeeper.cs"), window = Read("UI/SettingsWindow.cs"), panel = Read("Hud/HudPanel.cs"), council = Read("Hud/HudCouncil.cs");
        string keys = Read("UI/CouncilKeys.cs"), keyDefs = File.ReadAllText(Path.Combine(root, "Defs", "Misc", "Council_KeyBindings.xml"));
        C(keeper.Contains("PortraitFolder.GameStarted();") && folder.Contains("if (reading || Time.realtimeSinceStartup - readAt < FreshFor) return;") && folder.Contains("readAt = Time.realtimeSinceStartup;")
          && folder.Contains("public const float FreshFor = 60f;"),
            "the library read again on a worker when a game starts (new or loaded), not twice after the game's own start-up reading");
        C(!folder.Contains("LastWriteTime") && !folder.Contains("FileSystemWatcher"), "nothing looked at while playing (the player: \"i'm a bit concerned about lags and performance\")");
        C(folder.Contains("CC_PortraitFolder_ReadAgain") && folder.Contains("if (now != before)") && keyed.Contains("<CC_PortraitFolder_ReadAgain>"), "a short word only where the library changed since its last reading");
        C(window.Contains("public static void Open(string section = null)") && window.Contains("new Dialog_ModSettings(mod)") && window.Contains("jumpTo = at;"),
            "Rimfolk's settings opened from the game, at a section where given");
        C(panel.Contains("() => SettingsWindow.Open()") && council.Contains("protected override float HeaderButtons(Rect bar, float right)") && council.Contains("SettingsWindow.Open()")
          && inspect.Contains("SettingsWindow.Open(\"CC_SetSec_Portraits\")") && keys.Contains("DefDatabase<KeyBindingDef>.GetNamedSilentFail(\"Council_Key_Settings\")")
          && keyDefs.Contains("<defName>Council_Key_Settings</defName>"),
            "the shortcuts: every panel's menu, the Council panel's bar, the large portrait's menu (at the portraits), a key of the player's choosing (none at first)");
        return n;
    }
}
