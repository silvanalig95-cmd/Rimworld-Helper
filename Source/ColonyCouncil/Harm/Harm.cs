using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Hurts from everyday life: the one way a Rimfolk system hurts someone (DESIGN.md, "Harm from everyday life"). It
    /// keeps the harm review's shared rules (HarmMath.cs has the pure part):
    /// <list type="bullet">
    /// <item><see cref="Scale"/>: the setting "Hurts from everyday life" (0 to 200%) times the storyteller's own threat
    /// and disease numbers. Multiply every base chance by it, or use <see cref="Chance"/> and <see cref="Roll"/>.</item>
    /// <item><see cref="May"/> and <see cref="Bar"/>: the shared cap. At most one Rimfolk-made hurt per colonist in three
    /// days; none while downed, in a sickbed, pregnant or already fighting; babies never; children only
    /// <see cref="HurtClass.Mild"/>.</item>
    /// <item><see cref="Injure"/>: one of the game's own wounds (a bruise, a cut, a burn, a crush, a crack, a scratch) on a
    /// hand, an arm, a foot, a leg or the torso, never more than the part has left: nothing here kills outright. The
    /// tough take half, by the game's own damage factor. The game then bleeds, tends, infects and scars it as any
    /// wound.</item>
    /// <item><see cref="Ail"/>: a hediff (one of Rimfolk's ailments, the game's flu) under the same cap, optionally in
    /// place of another (a strained back turned into a torn back).</item>
    /// <item><see cref="Fight"/>: the game's own social fight between two grown-ups, under the cap for both.</item>
    /// <item><see cref="Note"/>: a hurt that came another way (a bout's hard blow, a guest's brawl) counted for the cap.</item>
    /// <item>The shared budget (<see cref="PassOf"/>, HarmBudgetMath.cs): every roll and every hurt names its kind
    /// (<see cref="HarmKinds"/>). A kind's chance is its base rate times <see cref="Scale"/> times its pass, which shares the
    /// budget of the kinds built before it among the kinds switched on, a kind that came lately passing less often.</item>
    /// </list>
    /// Every one of these records the hurt for the cap and its kind for the budget (saved, <see cref="HarmBook"/>). The caller tells the player (a
    /// bubble through Speech, a message through Tell at Importance.Important, as harm always is) and names the risk
    /// before any choice that brings it (an impact line, a decree's card: <see cref="DecreeLine"/>). No mood is added on
    /// top of the game's pain.
    /// </summary>
    public static class Harm
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The setting, 0 to 2.</summary>
        public static float Setting => HarmMath.Clamp(S?.everydayHurts ?? HarmMath.DefaultSetting, HarmMath.MinSetting, HarmMath.MaxSetting);

        /// <summary>Hurts from everyday life happen at all (the setting above nothing).</summary>
        public static bool On => Setting > 0.001f;

        /// <summary>The storyteller's lean: about two fifths with no threats and diseases half as often, a quarter more in the hardest.</summary>
        public static float Lean
        {
            get
            {
                var d = Find.Storyteller?.difficulty;
                return HarmMath.Lean(d?.threatScale ?? 1f, d?.diseaseIntervalFactor ?? 1f);
            }
        }

        /// <summary>The factor on every rate: the setting times the storyteller's lean.</summary>
        public static float Scale
        {
            get
            {
                var d = Find.Storyteller?.difficulty;
                return HarmMath.Scale(Setting, d?.threatScale ?? 1f, d?.diseaseIntervalFactor ?? 1f);
            }
        }

        /// <summary>
        /// The factor on a kind's rate: the setting, the storyteller's lean and the kind's pass within the shared budget.
        /// For a chance the game works out itself (a hunted beast's revenge) or a rule that takes a scale.
        /// </summary>
        public static float ScaleFor(HarmKind kind) => Scale * PassOf(kind);

        /// <summary>A kind's base chance after the scale and its pass.</summary>
        public static float Chance(HarmKind kind, float baseChance) => HarmMath.Chance(baseChance, ScaleFor(kind));

        /// <summary>One roll of a kind's base chance after the scale and its pass (no cap: see the overload with a pawn).</summary>
        public static bool Roll(HarmKind kind, float baseChance) => Rand.Value < Chance(kind, baseChance);

        /// <summary>One roll for someone: they may be hurt that way now (the cap), and the kind's chance comes up.</summary>
        public static bool Roll(Pawn p, HarmKind kind, float baseChance, HurtClass c = HurtClass.Injury) => May(p, c) && Roll(kind, baseChance);

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ---------------- The shared budget ----------------

        private static readonly float[] passes = new float[HarmKinds.All.Length];
        private static readonly bool[] onNow = new bool[HarmKinds.All.Length];
        private static readonly float[] daysNow = new float[HarmKinds.All.Length];
        private static int passAt = int.MinValue;
        private static Game passGame;
        private static float budgetNow, sharedNow;

        /// <summary>A kind came, or the settings changed: the passes are worked out again at the next roll.</summary>
        public static void BudgetChanged() => passAt = int.MinValue;

        /// <summary>
        /// A kind's pass within the shared budget now (HarmBudgetMath): 1 with only the kinds built before it on and none
        /// come lately; less once later kinds share it, and less again for a kind that came in the last fifteen days. It
        /// doesn't depend on the setting or the storyteller, which <see cref="Scale"/> brings in. Worked out every 250
        /// ticks, and again once a kind has come.
        /// </summary>
        public static float PassOf(HarmKind kind)
        {
            if (kind == null) return 1f;
            int now = Now;
            if (passGame != Current.Game || passAt == int.MinValue || now < passAt || now - passAt >= 250) Reckon(now);
            int i = Array.IndexOf(HarmKinds.All, kind);
            return i >= 0 ? passes[i] : 1f;
        }

        private static void Reckon(int now)
        {
            passGame = Current.Game;
            passAt = now;
            var book = HarmBook.Instance;
            float budget = 0f, shared = 0f;
            var all = HarmKinds.All;
            for (int i = 0; i < all.Length; i++)
            {
                var k = all[i];
                daysNow[i] = HarmBudgetMath.DaysSince(book?.LastOf(k) ?? HarmMath.Never, now);
                onNow[i] = SystemOnSafe(k.system);
                if (!onNow[i]) continue;
                float w = Math.Max(0f, k.weight);
                if (k.founding) budget += w;
                shared += w * HarmBudgetMath.Variety(daysNow[i]);
            }
            budgetNow = budget;
            sharedNow = shared;
            for (int i = 0; i < all.Length; i++) passes[i] = HarmBudgetMath.PassFor(HarmBudgetMath.Variety(daysNow[i]), budget, shared);
        }

        /// <summary>For the dev tools: each kind, whether its system is on, days since it came, its pass; and the budget.</summary>
        public static string BudgetReport()
        {
            PassOf(HarmKinds.Drills);
            var sb = new System.Text.StringBuilder();
            sb.Append("[Rimfolk] the shared budget of hurts from everyday life: ").Append(budgetNow.ToString("0.000"))
                .Append(" a colonist-year at 100% (the kinds on, weighed by their variety: ").Append(sharedNow.ToString("0.000"))
                .Append("); the setting and the storyteller's lean ").Append(Scale.ToString("0.00")).AppendLine(".");
            var all = HarmKinds.All;
            for (int i = 0; i < all.Length; i++)
                sb.Append("  ").Append(all[i].label).Append(": ").Append(onNow[i] ? "on" : "off").Append(", base ").Append(all[i].weight.ToString("0.000"))
                    .Append(daysNow[i] >= HarmBudgetMath.VarietyDays ? "" : ", came " + daysNow[i].ToString("0.0") + " days ago")
                    .Append(", pass ").Append(passes[i].ToString("0.00")).AppendLine();
            return sb.ToString();
        }

        private static bool SystemOnSafe(string system)
        {
            try
            {
                return SystemOn(system);
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Whether the system a kind sits under is on in this game: its setting, and for a soft integration, its mod. A
        /// kind whose system is off takes no share of the budget, so the rest are left as they would be without it. A later
        /// kind with a system of its own adds its case here.
        /// </summary>
        public static bool SystemOn(string system)
        {
            var s = S;
            if (s == null) return true;
            switch (system)
            {
                case "drills": return PlayOuts.On;
                case "ailments": return AilmentTracker.On;
                case "camps": return CampTracker.Enabled;
                case "journeys": return JourneyBook.On;
                case "champion": return Outsiders.SparOn;
                case "hunts": return PlayOuts.On && RimfolkPatches.On;
                case "rivals": return PlayOuts.On && s.socialWeb;
                case "elections": return s.offices && s.electionEvent;
                case "heart": return Heart.On;
                case "herds": return GreatHerds.On;
                case "feasts": return Feasts.On;
                case "schemes": return Schemes.On;
                case "pokemon": return PokeWorldLink.Active && s.pokemonTypeSpar;
                // Harm, part 4.
                case "physician": return AilmentTracker.On && RimfolkPatches.On;
                case "guestCough": return Visitors.On && AilmentTracker.On && Interplay.On;
                case "coldNight": return AilmentTracker.On && Interplay.On;
                case "felling": return AilmentTracker.On && RimfolkPatches.On;
                case "failedBuild": return Setbacks.On(SetbackGroup.Life) && RimfolkPatches.On;
                case "hotBench": return HotBench.On;
                case "rut": return AnimalLife.On;
                case "herdLesson": return Interplay.On && s.offices && PlayOuts.On;
                case "saddle": return AnimalLife.On && AnimalFacts.GiddyUp;
                case "groups": return GroupLife.Active;
                case "guestNight": return RimfolkPatches.On && Visitors.On;
                case "guestNightHospitality": return RimfolkPatches.On && Quarrels.HospitalityLoaded;
                case "rematch": return Outsiders.On && (Outsiders.DuelOn || Outsiders.SparOn);
                case "games": return ColonyCalendar.On && PlayOuts.On;
                // The decrees, Rimfolk's events (dangers on the map, the hail) and the peoples' callers are always in the game.
                default: return true;
            }
        }

        // ---------------- The shared cap ----------------

        /// <summary>Whether someone may take a Rimfolk-made hurt of this class now.</summary>
        public static bool May(Pawn p, HurtClass c = HurtClass.Injury) => Bar(p, c) == HurtBar.None;

        /// <summary>Why someone may not take a Rimfolk-made hurt of this class now (None: they may). Only the colony's own and its guests are asked.</summary>
        public static HurtBar Bar(Pawn p, HurtClass c = HurtClass.Injury)
        {
            if (p?.health?.hediffSet == null || p.Dead || p.RaceProps == null || !p.RaceProps.Humanlike) return HurtBar.Off;
            int now = Now;
            var stage = p.DevelopmentalStage;
            return HarmMath.Bar(Scale, stage.Baby(), stage.Juvenile() && !stage.Baby(), p.Downed, Sickbed(p), Pregnant(p), Fighting(p, now),
                HarmBook.Instance?.Last(p) ?? HarmMath.Never, now, c);
        }

        /// <summary>
        /// The cap for someone whose own attack brings the hurt about (a hunter's shot at game that can turn,
        /// Harm/HuntsBite.cs): that shot in the last hour doesn't count as already fighting; drafted, a fight or a rage, a
        /// melee threat or a hurt in the last hour still do.
        /// </summary>
        public static bool MayAtOwnAttack(Pawn p, HurtClass c = HurtClass.Injury)
        {
            if (p?.health?.hediffSet == null || p.Dead || p.RaceProps == null || !p.RaceProps.Humanlike) return false;
            int now = Now;
            var stage = p.DevelopmentalStage;
            bool fighting = Fighting(p, now, false);
            return HarmMath.Bar(Scale, stage.Baby(), stage.Juvenile() && !stage.Baby(), p.Downed, Sickbed(p), Pregnant(p), fighting,
                HarmBook.Instance?.Last(p) ?? HarmMath.Never, now, c) == HurtBar.None;
        }

        /// <summary>Days since their last Rimfolk-made hurt (a large number when there was none).</summary>
        public static float DaysSince(Pawn p)
        {
            int last = HarmBook.Instance?.Last(p) ?? HarmMath.Never;
            if (last == HarmMath.Never || last > Now) return 9999f;
            return (Now - last) / (float)GenDate.TicksPerDay;
        }

        /// <summary>
        /// A hurt that came some other way (a bout's hard blow, a brawl), counted for the cap and for its kind's turn in the
        /// budget. <paramref name="kind"/> null: counted for the cap only (the outsiders' practice bout, whose one blow in
        /// 25 is the game's own sparring rate and the anchor of all the rest).
        /// </summary>
        public static void Note(Pawn p, HarmKind kind)
        {
            if (p == null) return;
            var book = HarmBook.Instance;
            if (book == null) return;
            book.Record(p, Now);
            if (kind != null) book.Came(kind, Now);
        }

        /// <summary>
        /// In a sickbed, or ought to be: in a medical bed or resting in one, bleeding, or ill with something that can kill
        /// (the flu, the plague, an infection). A small thing still to be tended (a strained back, last week's cut) is not
        /// a sickbed: people work on with those, and some of the hurts here are about just that.
        /// </summary>
        public static bool Sickbed(Pawn p)
        {
            try
            {
                if (p.CurJobDef == JobDefOf.LayDownResting) return true;
                if (p.InBed() && (RestUtility.CurrentBed(p)?.Medical ?? false)) return true;
                var set = p.health.hediffSet;
                if (set.BleedRateTotal > 0.01f) return true;
                var hs = set.hediffs;
                for (int i = 0; i < hs.Count; i++)
                    if (!(hs[i] is Hediff_Injury) && hs[i].Visible && hs[i].def.lethalSeverity > 0f) return true;
                return false;
            }
            catch
            {
                return p.InBed();
            }
        }

        /// <summary>With child: the game's pregnancy, or a mod's that names itself the same way.</summary>
        public static bool Pregnant(Pawn p)
        {
            var hs = p.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
                if (hs[i].def?.defName != null && hs[i].def.defName.StartsWith("Pregnant", StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Already fighting: drafted, in a fight or a rage, at an attack, or hurt or attacking in the last hour.</summary>
        public static bool Fighting(Pawn p, int now) => Fighting(p, now, true);

        /// <summary>The same; without <paramref name="ownAttack"/>, their own attack in the last hour (a hunter's shot) left out.</summary>
        private static bool Fighting(Pawn p, int now, bool ownAttack)
        {
            if (p.Drafted) return true;
            var ms = p.MentalStateDef;
            if (ms != null && (ms == MentalStateDefOf.SocialFighting || ms.IsAggro)) return true;
            var d = p.CurJobDef;
            if (d == JobDefOf.AttackMelee || ownAttack && d == JobDefOf.AttackStatic) return true;
            var mind = p.mindState;
            if (mind == null) return false;
            if (mind.meleeThreat != null) return true;
            if (mind.lastHarmTick > 0 && now - mind.lastHarmTick < HarmMath.FreshHarmTicks) return true;
            return ownAttack && mind.lastAttackTargetTick > 0 && now - mind.lastAttackTargetTick < HarmMath.FreshHarmTicks;
        }

        // ---------------- Wounds ----------------

        private static readonly Dictionary<HurtKind, HediffDef> injuries = new Dictionary<HurtKind, HediffDef>();
        private static Game injuriesGame;

        /// <summary>The game's own injury for a kind (Bruise, Cut, Burn, Crush, Crack, Scratch), or null.</summary>
        public static HediffDef InjuryOf(HurtKind kind)
        {
            if (injuriesGame != Current.Game)
            {
                injuries.Clear();
                injuriesGame = Current.Game;
            }
            if (injuries.TryGetValue(kind, out var def)) return def;
            def = kind == HurtKind.Cut ? HediffDefOf.Cut : DefDatabase<HediffDef>.GetNamedSilentFail(kind.ToString());
            injuries[kind] = def;
            return def;
        }

        /// <summary>The game's own damage that makes that wound, for the red flash and the record.</summary>
        private static DamageDef DamageOf(HurtKind kind)
        {
            switch (kind)
            {
                case HurtKind.Cut: return DamageDefOf.Cut;
                case HurtKind.Burn: return DamageDefOf.Burn;
                case HurtKind.Crush: return DamageDefOf.Crush;
                case HurtKind.Scratch: return DamageDefOf.Scratch;
                default: return DamageDefOf.Blunt;
            }
        }

        private static readonly string[] Hands = { "Hand" }, Arms = { "Arm", "Shoulder" }, Feet = { "Foot" }, Legs = { "Leg" }, Torsos = { "Torso" };
        private static readonly string[] LegBones = { "Tibia", "Femur" }, ArmBones = { "Radius", "Humerus" }, TorsoBones = { "Sternum", "Clavicle" };

        /// <summary>
        /// Where the wound lands: a part of the kind asked for that they still have (a crack on one of its bones), never a
        /// vital one, the head or the neck; with none of those, any arm, leg or the torso.
        /// </summary>
        public static BodyPartRecord PartFor(Pawn p, HurtKind kind, HurtWhere where)
        {
            var parts = p.health.hediffSet.GetNotMissingParts().ToList();
            string[] names;
            switch (where)
            {
                case HurtWhere.Hand: names = kind == HurtKind.Crack ? ArmBones : Hands; break;
                case HurtWhere.Arm: names = kind == HurtKind.Crack ? ArmBones : Arms; break;
                case HurtWhere.Foot: names = kind == HurtKind.Crack ? LegBones : Feet; break;
                case HurtWhere.Leg: names = kind == HurtKind.Crack ? LegBones : Legs; break;
                case HurtWhere.Torso: names = kind == HurtKind.Crack ? TorsoBones : Torsos; break;
                default: names = kind == HurtKind.Crack ? LegBones.Concat(ArmBones).ToArray() : Arms.Concat(Legs).Concat(Torsos).ToArray(); break;
            }
            var fit = parts.Where(x => names.Contains(x.def.defName) && Safe(x)).ToList();
            if (fit.Count == 0 && where == HurtWhere.Hand) fit = parts.Where(x => Arms.Contains(x.def.defName) && Safe(x)).ToList();
            if (fit.Count == 0 && where == HurtWhere.Foot) fit = parts.Where(x => Legs.Contains(x.def.defName) && Safe(x)).ToList();
            if (fit.Count == 0 && kind != HurtKind.Crack)
                fit = parts.Where(x => (Arms.Contains(x.def.defName) || Legs.Contains(x.def.defName) || Torsos.Contains(x.def.defName)) && Safe(x)).ToList();
            return fit.Count == 0 ? null : fit.RandomElement();
        }

        private static bool Safe(BodyPartRecord x)
        {
            var tags = x.def.tags;
            if (tags != null)
                for (int i = 0; i < tags.Count; i++)
                    if (tags[i] != null && tags[i].vital)
                        return false;
            return x.def.defName != "Neck" && x.def.defName != "Head";
        }

        /// <summary>
        /// One of the game's own wounds, sized between <paramref name="low"/> and <paramref name="high"/> (times the
        /// body's damage factor: the tough take half), on a part of the kind asked for, under the cap. Never more than the
        /// part has left but one point. Shown as the game shows a hit (the red flash, blood for a cut or a crush). Returns
        /// the wound (or the one it merged into), or null when nothing was given.
        /// </summary>
        public static Hediff_Injury Injure(Pawn p, HurtKind kind, HurtWhere where, float low, float high, HarmKind why, HurtClass c = HurtClass.Injury)
        {
            try
            {
                if (!May(p, c)) return null;
                var def = InjuryOf(kind);
                if (def == null) return null;
                var part = PartFor(p, kind, where);
                if (part == null) return null;
                float factor = 1f;
                try
                {
                    factor = p.GetStatValue(StatDefOf.IncomingDamageFactor);
                }
                catch
                {
                    // A modded stat that can't answer: the plain size.
                }
                float size = HarmMath.SafeSeverity(HarmMath.Severity(low, high, Rand.Value, factor), p.health.hediffSet.GetPartHealth(part));
                if (size < HarmMath.SmallestWound) return null;
                var h = (Hediff_Injury)HediffMaker.MakeHediff(def, p, part);
                h.Severity = size;
                var dinfo = new DamageInfo(DamageOf(kind), size, 0f, -1f, null, part, null, DamageInfo.SourceCategory.ThingOrUnknown, null, false);
                p.health.AddHediff(h, part, dinfo);
                var given = p.health.hediffSet.hediffs.Contains(h) ? h
                    : p.health.hediffSet.hediffs.OfType<Hediff_Injury>().FirstOrDefault(x => x.def == def && x.Part == part);
                if (given == null) return null;
                Note(p, why);
                if (p.Spawned)
                {
                    p.Drawer?.Notify_DamageApplied(dinfo);
                    if ((kind == HurtKind.Cut || kind == HurtKind.Crush || kind == HurtKind.Scratch) && p.RaceProps.BloodDef != null)
                        FilthMaker.TryMakeFilth(p.Position, p.Map, p.RaceProps.BloodDef, p.LabelIndefinite());
                    else if (kind == HurtKind.Burn) FleckMaker.ThrowSmoke(p.DrawPos, p.Map, 0.6f);
                }
                return given;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a wound", e);
                return null;
            }
        }

        /// <summary>
        /// A hediff (one of Rimfolk's ailments, the game's flu) at a severity, under the cap; with
        /// <paramref name="replaces"/>, in place of that one (a strained back turned into a torn back). The caller tells it.
        /// Returns the hediff, or null when nothing was given.
        /// </summary>
        public static Hediff Ail(Pawn p, HediffDef def, float severity, HarmKind why, HurtClass c = HurtClass.Injury, Hediff replaces = null)
        {
            try
            {
                if (def == null || !May(p, c) || p.health.hediffSet.HasHediff(def)) return null;
                var h = HediffMaker.MakeHediff(def, p);
                h.Severity = Math.Max(0.01f, severity);
                if (replaces != null && p.health.hediffSet.hediffs.Contains(replaces)) p.health.RemoveHediff(replaces);
                p.health.AddHediff(h);
                if (!p.health.hediffSet.hediffs.Contains(h)) return null;
                Note(p, why);
                return h;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: an ailment", e);
                return null;
            }
        }

        /// <summary>
        /// The game's own social fight between two grown-ups who may both be hurt by one (the cap for both, neither unable
        /// to do violence), recorded for both. The game says so in its own message. Returns whether it began.
        /// </summary>
        public static bool Fight(Pawn a, Pawn b, HarmKind why)
        {
            try
            {
                if (a?.interactions == null || b == null || a == b || !May(a, HurtClass.Fight) || !May(b, HurtClass.Fight)) return false;
                if (a.WorkTagIsDisabled(WorkTags.Violent) || b.WorkTagIsDisabled(WorkTags.Violent)) return false;
                // together: the caller brings the two face to face; the game's own fight needs them near each other.
                a.interactions.StartSocialFight(b);
                if (a.MentalStateDef != MentalStateDefOf.SocialFighting) return false;
                Note(a, why);
                Note(b, why);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm: a fight", e);
                return false;
            }
        }

        // ---------------- Named before the choice ----------------

        /// <summary>A decree's card and letter: what it can do to the body while it holds, in words (null: nothing, or hurts are off).</summary>
        public static string DecreeLine(DecreeDef def)
        {
            if (def == null || def.hurts == DecreeHurt.None || !On) return null;
            // The bouts are on the map: with answers kept off it, the drills stay practice alone.
            if ((def.hurts == DecreeHurt.Drills || def.hurts == DecreeHurt.Muster) && !PlayOuts.On) return null;
            // The frail's chill comes with the everyday ailments and "Systems talk to each other".
            if (def.hurts == DecreeHurt.ColdNights && !SystemOn(HarmKinds.ColdNight.system)) return null;
            return ("CC_Harm_Decree_" + def.hurts).Translate().Resolve();
        }
    }

    /// <summary>
    /// The harm review's saved part: each colonist's last Rimfolk-made hurt (by thingIDNumber), for the shared cap, kept
    /// only for three days; the evening each home last drilled; the hunts' game that can turn while it's remembered
    /// (HuntsBite.cs, six days); travellers' stays at sick towns and the sickness they carry home (SickHome.cs, while the
    /// road home lasts and until it shows). The per-colonist hour of the work hurts, the evening's drill, the sick towns'
    /// look and a rival's sabotage run from here (WorkHurts.cs, DrillBouts.cs, SickHome.cs, Sabotage.cs).
    /// </summary>
    public class HarmBook : GameComponent
    {
        /// <summary>thingIDNumber: the tick of their last Rimfolk-made hurt (pruned past three days).</summary>
        private Dictionary<int, int> lastHurt = new Dictionary<int, int>();
        /// <summary>A home map's uniqueID: the game day its last evening drill was called.</summary>
        private Dictionary<int, int> drillDay = new Dictionary<int, int>();
        /// <summary>An animal's thingIDNumber: the tick a hunt marked it as game that can turn (HuntsBite.cs), pruned past six days.</summary>
        private Dictionary<int, int> turning = new Dictionary<int, int>();
        /// <summary>Of those, the ones whose turn within the shared budget didn't come up this hunt: calm under its hunters (pruned with them).</summary>
        private HashSet<int> calm = new HashSet<int>();
        /// <summary>Travellers' stays at sick towns, and the sickness one carries home until it shows (SickHome.cs).</summary>
        public List<SickStay> sick = new List<SickStay>();
        /// <summary>The shared budget's memory: a kind's key, the tick it last hurt someone (pruned past fifteen days, when it has its whole share back).</summary>
        private Dictionary<string, int> kindLast = new Dictionary<string, int>();
        /// <summary>Hurts from everyday life still to come (harm, part 4): a sickness caught and showing in a day or two, a sore loser, a rematch owed.</summary>
        public List<HarmWaiting> waiting = new List<HarmWaiting>();
        private List<int> tmpKeys, tmpValues, tmpKeys2, tmpValues2, tmpKeys3, tmpValues3, tmpValues4;
        private List<string> tmpKinds;

        private static HarmBook cached;
        private static Game cachedGame;

        public static HarmBook Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<HarmBook>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public HarmBook(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            WorkHurts.Reset();
            DrillBouts.Reset();
            Sabotage.Reset();
            HarmFour.Reset();
            Harm.BudgetChanged();
        }

        public int Last(Pawn p) => p != null && lastHurt.TryGetValue(p.thingIDNumber, out int t) ? t : HarmMath.Never;

        public void Record(Pawn p, int now)
        {
            if (p != null) lastHurt[p.thingIDNumber] = now;
        }

        /// <summary>The tick a kind last hurt someone (HarmMath.Never: not in the last fifteen days).</summary>
        public int LastOf(HarmKind kind) => kind != null && kindLast.TryGetValue(kind.key, out int t) ? t : HarmMath.Never;

        /// <summary>A kind hurt someone: it rests a while within the budget (HarmBudgetMath.Variety).</summary>
        public void Came(HarmKind kind, int now)
        {
            if (kind == null) return;
            kindLast[kind.key] = now;
            Harm.BudgetChanged();
        }

        /// <summary>The evening drill already called today at this home.</summary>
        public bool DrilledToday(Map map, int day) => map != null && drillDay.TryGetValue(map.uniqueID, out int d) && d == day;

        public void Drilled(Map map, int day)
        {
            if (map != null) drillDay[map.uniqueID] = day;
        }

        /// <summary>Any of the hunts' game that can turn is remembered (a quick look for the game's damage hooks).</summary>
        public bool AnyTurning => turning.Count > 0;

        public bool IsTurning(Pawn a) => a != null && turning.TryGetValue(a.thingIDNumber, out int t) && HarmTwoMath.TurningKept(t, Find.TickManager.TicksGame);

        public void Turning(Pawn a, int now)
        {
            if (a != null) turning[a.thingIDNumber] = now;
        }

        public void NotTurning(Pawn a)
        {
            if (a == null) return;
            turning.Remove(a.thingIDNumber);
            calm.Remove(a.thingIDNumber);
        }

        /// <summary>A hunt's head that can turn whose turn within the budget didn't come up: it stays calm under its hunters.</summary>
        public void Calm(Pawn a)
        {
            if (a != null) calm.Add(a.thingIDNumber);
        }

        public bool IsCalm(Pawn a) => a != null && calm.Count > 0 && calm.Contains(a.thingIDNumber);

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            // A tenth of the colony every 250 ticks, each colonist once an hour (Sched.RareHarm); the colony's part (the
            // evening drill, the day's pruning) rides on the first slice.
            int slice = Sched.SliceTurn(now, Sched.HourTicks, Sched.Slices, Sched.RareHarm);
            if (slice < 0 || !Harm.On) return;
            using var clock = PassClock.Time(Pass.RareHarm);
            try
            {
                if (slice == 0)
                {
                    DrillBouts.Hourly(this, now);
                    // Sickness carried home from a sick town, and a rival's sabotage (harm, parts 2 and 3).
                    SickHome.Hourly(this, now);
                    Sabotage.Hourly(this, now);
                    // Harm, part 4: what was caught showing, the waiting walks and visits, the hourly looks.
                    HarmFour.Hourly(this, now);
                    if (Sched.FirstHourOfDay(now)) Prune(now);
                }
                WorkHurts.Slice(slice, now);
                HarmFour.Slice(this, slice, now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("harm", e);
            }
        }

        private void Prune(int now)
        {
            foreach (var k in lastHurt.Where(kv => !HarmMath.Lately(kv.Value, now)).Select(kv => kv.Key).ToList()) lastHurt.Remove(k);
            var maps = new HashSet<int>(Find.Maps.Select(m => m.uniqueID));
            foreach (var k in drillDay.Keys.Where(k => !maps.Contains(k)).ToList()) drillDay.Remove(k);
            foreach (var k in turning.Where(kv => !HarmTwoMath.TurningKept(kv.Value, now)).Select(kv => kv.Key).ToList()) turning.Remove(k);
            calm.RemoveWhere(k => !turning.ContainsKey(k));
            foreach (var k in kindLast.Where(kv => HarmBudgetMath.Forgotten(kv.Value, now) || HarmKinds.Of(kv.Key) == null).Select(kv => kv.Key).ToList()) kindLast.Remove(k);
            waiting.RemoveAll(x => x == null || x.Lapsed(now));
            SickHome.Prune(this, now);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving && Find.TickManager != null) Prune(Find.TickManager.TicksGame);
            Scribe_Collections.Look(ref lastHurt, "lastHurt", LookMode.Value, LookMode.Value, ref tmpKeys, ref tmpValues);
            Scribe_Collections.Look(ref drillDay, "drillDay", LookMode.Value, LookMode.Value, ref tmpKeys2, ref tmpValues2);
            Scribe_Collections.Look(ref turning, "turning", LookMode.Value, LookMode.Value, ref tmpKeys3, ref tmpValues3);
            Scribe_Collections.Look(ref calm, "calm", LookMode.Value);
            Scribe_Collections.Look(ref sick, "sick", LookMode.Deep);
            Scribe_Collections.Look(ref kindLast, "kindLast", LookMode.Value, LookMode.Value, ref tmpKinds, ref tmpValues4);
            Scribe_Collections.Look(ref waiting, "waiting", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                kindLast ??= new Dictionary<string, int>();
                waiting ??= new List<HarmWaiting>();
                waiting.RemoveAll(x => x == null || x.Broken);
                Harm.BudgetChanged();
                lastHurt ??= new Dictionary<int, int>();
                drillDay ??= new Dictionary<int, int>();
                turning ??= new Dictionary<int, int>();
                calm ??= new HashSet<int>();
                sick ??= new List<SickStay>();
                sick.RemoveAll(x => x == null || x.pawn == null);
            }
        }
    }
}
