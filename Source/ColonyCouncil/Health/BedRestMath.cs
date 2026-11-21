namespace ColonyCouncil
{
    /// <summary>
    /// The rule of bed rest only when it makes sense (Health/BedRest.cs): when a condition sends someone to bed, and when
    /// they keep working with it (sleeping a little more at night where it tires them). Pure (no game types): Tools/Tests
    /// checks it on every family of conditions and on each of Rimfolk's ailments from its def. The player's word on it
    /// (8 November 2026): "pawns should only bed rest if it's a serious condition, they have strong pain, they are bleeding
    /// etc., not for 'minor' problems. perhaps they sleep a bit more, but not all day long."
    /// <para>
    /// The game sends anyone to bed rest who has a tended wound still healing, however small, or an illness building
    /// immunity, however mild (HealthAIUtility.ShouldSeekMedicalRest), and keeps them there until it is healed or they are
    /// immune: a tended scratch or a head cold means days in bed. Under this rule only these do:
    /// </para>
    /// <list type="bullet">
    /// <item>A serious wound while it heals: serious by medicine by what is treated (heavy bleeding, a vital part or an
    /// organ, a deep wound), or wounds that together bring strong pain (<see cref="StrongPain"/>), or that leave moving or
    /// manipulation poor (<see cref="PoorLevel"/>) while one of them is on a leg or an arm.</item>
    /// <item>A serious illness: one that can kill (a lethal severity or a stage that threatens life) while they aren't yet
    /// immune, or a stage that brings strong pain or takes <see cref="SeriousDrop"/> or more off consciousness, moving or
    /// breathing.</item>
    /// <item>One of Rimfolk's ailments from the severity its def names (<c>restFrom</c>: a serious illness from the stage it
    /// is really underway, its first fever worked through); without it, the rule above.</item>
    /// <item>What the game makes urgent: down, an operation due, labour, and anything to tend now, except that someone
    /// with nothing serious to tend, not losing blood, keeps working until a doctor is free to tend them.</item>
    /// </list>
    /// What is to tend comes in three kinds (<see cref="Tend"/>): serious, which takes them to bed to be tended at once;
    /// what tending decides (<see cref="Matters"/>: an illness building immunity, a wound that may still get infected,
    /// one of Rimfolk's ailments that can turn into something worse), which the doctor sees to before small things; and
    /// small things (a bruise, a small cut, a sunburn).
    /// Everything else, they keep working with it, and its pain, capacities and mood go on costing them; a stage that tires
    /// (a cold, a fever's first days, a sprain) has them sleep a little more at night (<see cref="Sleeps"/>), never all day
    /// (<see cref="MaxRestFallWorking"/>). For the game's and other mods' conditions the rule only ever takes away rest the
    /// game would give: a condition the game doesn't rest for is left as it is. The player's own order to bed and the
    /// council's "rest when ill" decree are the game's rest, and more.
    /// </summary>
    public static class BedRestMath
    {
        /// <summary>
        /// Strong pain: the game's own line past "minor pain" (ThoughtWorker_Pain: "serious pain" from 15%). Wounds that
        /// together hurt this much, or an illness's stage that does (a torn back, a migraine at its worst, a kidney stone
        /// passing), need bed; below it the pain is minor, and they work through it.
        /// </summary>
        public const float StrongPain = 0.15f;

        /// <summary>
        /// A stage that takes this much or more off consciousness, moving or breathing is a serious condition: it leaves the
        /// capacity poor, as the health tab calls a capacity under 70% (the plague at its worst takes 30% off consciousness).
        /// Less (a cold's stuffiness, a stomach bug's worst day, a sprain's limp) they work through.
        /// </summary>
        public const float SeriousDrop = 0.3f;

        /// <summary>
        /// Wounds that leave moving or manipulation poor, at 70% or less (the health tab's "poor"), with one of them on that
        /// kind of limb (a leg cut to half its health, both legs badly hurt, a hand and arm both wounded): too lame or clumsy
        /// to work while they heal.
        /// </summary>
        public const float PoorLevel = 0.7f;

        /// <summary>
        /// A stage they keep working with makes them sleep a little more when it makes their rest fall faster than the
        /// game's own (its restFallFactor over 1): to bed a little earlier, up a little later.
        /// </summary>
        public const float RestFallNormal = 1f;

        /// <summary>
        /// The most a stage they keep working with may tire them: rest falling half as fast again, about two hours more
        /// sleep a day (<see cref="SleepHours"/>: 9 against the game's 7), never all day.
        /// </summary>
        public const float MaxRestFallWorking = 1.5f;

        /// <summary>The rest an hour asleep in an ordinary bed gives against what an hour awake costs (Need_Rest: 3.81e-5 against 1.58e-5 a tick).</summary>
        public const float GainPerFall = 3.809524e-5f / 1.5833333e-5f;

        /// <summary>The game loses no blood below this (a tenth of the blood a day, MedicineChoiceMath.BleedsPerDay).</summary>
        public const float LosingBlood = MedicineChoiceMath.BleedsPerDay;

        private const float Eps = 1e-4f;

        /// <summary>Why a condition sends someone to bed.</summary>
        public enum Why
        {
            None,
            // A wound, while it heals
            SeriousWound,
            WoundsHurt,
            WoundsLame,
            WoundsClumsy,
            // An illness
            Lethal,
            Pain,
            Dazed,
            Unsteady,
            Breathless,
            // One of Rimfolk's, from the severity its def names
            Stage,
            // The council's rest when ill decree: the game's own rest
            Decree
        }

        /// <summary>What has someone in bed now, beyond any one condition: the first that holds.</summary>
        public enum InBed
        {
            No,
            Downed,
            Surgery,
            Labor,
            Condition,
            Tending,
            Ordered,
            Decree,
            /// <summary>Not under the rule (a baby, a creature not of flesh), or another mod's reason: the game's own rest.</summary>
            GameOwn
        }

        /// <summary>
        /// How a condition to tend now is seen to. The player (21 November 2026): "if it's just a bruise ... they don't
        /// necessarily have to prioritize attending as much ... But for these viruses and stuff that, you know, you have to
        /// develop immunity, or where tending really impacts whether the illness or whatever it is becomes permanent or
        /// escalates, it's important, I think, that they get tended and then go about their business if it allows."
        /// </summary>
        public enum Tend
        {
            /// <summary>Nothing to tend now.</summary>
            None,
            /// <summary>A small thing (a bruise, a small cut, a sunburn): lies down to be tended once a doctor is free, after the rest.</summary>
            Small,
            /// <summary>What tending decides (<see cref="Matters"/>): lies down once a doctor is free, and the doctor sees to it before small things.</summary>
            Matters,
            /// <summary>Serious by medicine by what is treated: to bed to be tended at once, a doctor free or not.</summary>
            Serious
        }

        /// <summary>Why tending decides how a condition goes.</summary>
        public enum Matters
        {
            No,
            /// <summary>An illness the body builds immunity to, not immune yet: tending slows it while the immunity comes.</summary>
            Immunity,
            /// <summary>
            /// A wound that may still get infected: the game rolls for an infection once, within the wound's first hours
            /// (HealthTuning.InfectionDelayRange), and a tended wound is less likely to turn.
            /// </summary>
            MayInfect,
            /// <summary>One of Rimfolk's ailments its def marks (tendingMatters): left untended it can lead to something worse.</summary>
            Worse
        }

        /// <summary>
        /// A stage of a condition, as the rule reads it: how much it takes off each capacity, its pain, and how fast it tires
        /// (the game's restFallFactor with its offset; 1 for none).
        /// </summary>
        public struct Stage
        {
            public float pain, consciousness, moving, breathing;
            public float restFall;
        }

        /// <summary>How much a capacity modifier takes off: its offset, its factor and a cap below the whole, whichever is most.</summary>
        public static float Drop(float offset, float postFactor, float setMax)
        {
            float d = -offset + (1f - postFactor);
            if (setMax < 1f && 1f - setMax > d) d = 1f - setMax;
            return d > 0f ? d : 0f;
        }

        /// <summary>A stage that has them sleep a little more: their rest falls faster than the game's own.</summary>
        public static bool Sleeps(Stage s) => s.restFall > RestFallNormal + Eps;

        /// <summary>
        /// About how many hours a day someone sleeps in an ordinary bed with their rest falling this much faster than the
        /// game's own: the share of the day asleep that keeps their rest level (7 hours at 1, 9 at 1.5, 11 at 2).
        /// </summary>
        public static float SleepHours(float restFall) => restFall <= 0f ? 0f : 24f * restFall / (GainPerFall + restFall);

        /// <summary>One condition, as the rule reads it.</summary>
        public struct Condition
        {
            /// <summary>A wound that heals (the game's injury, not a scar).</summary>
            public bool wound;
            /// <summary>The wound is tended: the game's bed rest counts it until it has healed.</summary>
            public bool tended;
            /// <summary>Medicine by what is treated's judgement (MedicineChoiceMath.Judge), with its reason; of a wound or an illness.</summary>
            public MedicineChoiceMath.Threat threat;
            public MedicineChoiceMath.Why threatWhy;
            /// <summary>It wants tending now (the game's TendableNow).</summary>
            public bool tendNow;
            /// <summary>A wound that may still get infected (<see cref="MayInfect"/>).</summary>
            public bool mayInfect;
            /// <summary>The wound is on a part for moving (a leg, a foot, the pelvis, the spine) or for handling things (an arm, a hand, a shoulder).</summary>
            public bool onLegs, onArms;
            /// <summary>Any other condition that shows (an illness, an ailment, a lasting condition).</summary>
            public bool illness;
            /// <summary>It builds immunity on its own (the game's bed rest counts it until immune), and they are immune now.</summary>
            public bool immunizable, immune;
            /// <summary>It can kill: a lethal severity, or a stage that threatens life.</summary>
            public bool lethal;
            public Stage stage;
            /// <summary>One of Rimfolk's ailments, and the severity its def names for bed rest (0: the rule above).</summary>
            public bool rimfolk;
            public float restFrom, severity;
            /// <summary>One of Rimfolk's ailments its def marks: left untended it can lead to something worse (tendingMatters).</summary>
            public bool worse;
            /// <summary>Taken off bed rest by hand (Smart Medicine's menu on the health tab).</summary>
            public bool ignored;
        }

        /// <summary>What the healing wounds do to someone together.</summary>
        public struct Body
        {
            /// <summary>The pain of the healing wounds, as the game feels it (its pain factors on top).</summary>
            public float woundPain;
            /// <summary>Their moving and manipulation now.</summary>
            public float moving, manipulation;
        }

        /// <summary>The game's own bed rest counts this condition: a tended wound still healing, an illness not yet immune.</summary>
        public static bool GameCounts(Condition c) => c.wound ? c.tended : c.illness && c.immunizable && !c.immune;

        /// <summary>
        /// Whether a condition, as it is now, needs bed by the rule, and why; for any condition, whether the game counts it or
        /// not (the health tab's line says it of an untended wound too).
        /// </summary>
        public static Why Own(Condition c, Body b)
        {
            if (c.ignored) return Why.None;
            if (c.rimfolk && c.restFrom > 0f) return c.severity >= c.restFrom - Eps ? Why.Stage : Why.None;
            if (c.wound)
            {
                if (c.threat == MedicineChoiceMath.Threat.Serious) return Why.SeriousWound;
                if (b.woundPain >= StrongPain - Eps) return Why.WoundsHurt;
                if (c.onLegs && b.moving <= PoorLevel + Eps) return Why.WoundsLame;
                if (c.onArms && b.manipulation <= PoorLevel + Eps) return Why.WoundsClumsy;
                return Why.None;
            }
            if (!c.illness) return Why.None;
            if (c.lethal && !c.immune) return Why.Lethal;
            return StageWhy(c.stage);
        }

        /// <summary>A stage that needs bed on its own: in strong pain, or too dazed, unsteady or short of breath to work.</summary>
        public static Why StageWhy(Stage s)
        {
            if (s.pain >= StrongPain - Eps) return Why.Pain;
            if (s.consciousness >= SeriousDrop - Eps) return Why.Dazed;
            if (s.moving >= SeriousDrop - Eps) return Why.Unsteady;
            if (s.breathing >= SeriousDrop - Eps) return Why.Breathless;
            return Why.None;
        }

        /// <summary>
        /// Whether this condition sends them to bed now. The game's and other mods' conditions only when the game's own bed
        /// rest counts them (the rule only takes rest away from those); Rimfolk's ailments by the rule alone (each reviewed,
        /// and some need bed at their worst where the game would give none).
        /// </summary>
        public static Why Sends(Condition c, Body b)
        {
            if (c.ignored) return Why.None;
            if (!c.rimfolk && !GameCounts(c)) return Why.None;
            return Own(c, b);
        }

        /// <summary>A condition they keep working with that has them sleep a little more (an illness whose stage tires them).</summary>
        public static bool SleepsMore(Condition c, Body b) => c.illness && Own(c, b) == Why.None && Sleeps(c.stage);

        /// <summary>
        /// A wound that may still get infected: untended, with a chance of turning that medicine by what is treated counts
        /// (<see cref="MedicineChoiceMath.MayInfectChance"/>, 3%: a small cut or a bruise has less), and young enough that
        /// the game's one roll for it may still come (<paramref name="windowTicks"/>, the longest delay of that roll).
        /// </summary>
        public static bool MayInfect(float chance, bool tended, int ageTicks, int windowTicks) =>
            !tended && chance >= MedicineChoiceMath.MayInfectChance && ageTicks >= 0 && ageTicks < windowTicks;

        /// <summary>Why tending decides how this condition goes (No: it doesn't, or not now).</summary>
        public static Matters WhyMatters(Condition c)
        {
            if (c.wound) return c.mayInfect ? Matters.MayInfect : Matters.No;
            if (!c.illness) return Matters.No;
            if (c.worse) return Matters.Worse;
            return c.immunizable && !c.immune ? Matters.Immunity : Matters.No;
        }

        /// <summary>How this condition is seen to now: nothing to tend, a small thing, what tending decides, or serious.</summary>
        public static Tend KindOf(Condition c)
        {
            if (!c.tendNow) return Tend.None;
            if (c.threat == MedicineChoiceMath.Threat.Serious) return Tend.Serious;
            return WhyMatters(c) != Matters.No ? Tend.Matters : Tend.Small;
        }

        /// <summary>
        /// The doctor's order among the patients waiting in bed (Health/BedRestPatches.cs): something serious first, then
        /// what tending decides, then small things; the closest first within each.
        /// </summary>
        public static float TendOrder(Tend t) => t == Tend.Serious ? 2f : t == Tend.Matters ? 1f : 0f;

        /// <summary>
        /// A look taken at tick <paramref name="at"/> still holds at <paramref name="now"/> for <paramref name="ticks"/>.
        /// Never one not yet taken (int.MinValue, where now minus it overflows an int) or from a later tick (another game).
        /// </summary>
        public static bool Fresh(int now, int at, int ticks) => at <= now && (long)now - at < ticks;

        /// <summary>Everything about the patient beyond their conditions, as the game and the player have it now.</summary>
        public struct Patient
        {
            /// <summary>Not under the rule: a baby, a creature not of flesh. The game's own rest.</summary>
            public bool exempt;
            /// <summary>The game's own answer (with any other mod's on it).</summary>
            public bool gameRest;
            /// <summary>The game's answer holds for one of its own two reasons the rule covers (a tended wound, an illness not yet immune).</summary>
            public bool gameOwnReasons;
            /// <summary>The player told them to bed (the medical bed's "rest until healed", or that menu being made).</summary>
            public bool ordered;
            /// <summary>The council's rest when ill decree is in force for them.</summary>
            public bool decree;
            public bool downed, surgery, labor;
            /// <summary>Something to tend now (the game's urgent mark), whether anything of it is serious (<see cref="Tend.Serious"/>), whether they lose blood.</summary>
            public bool needsTending, seriousToTend, bleeding;
            /// <summary>Someone can come and tend them now: a doctor free, and the medical care setting allows any.</summary>
            public bool doctorFree;
            /// <summary>Some condition sends them to bed (<see cref="Sends"/>).</summary>
            public bool conditionRests;
        }

        /// <summary>What has them in bed now (No: they keep working), in the order the reasons are told.</summary>
        public static InBed Decide(Patient p)
        {
            if (p.exempt) return p.gameRest ? InBed.GameOwn : InBed.No;
            if (p.downed) return InBed.Downed;
            if (p.surgery) return InBed.Surgery;
            if (p.labor) return InBed.Labor;
            if (p.conditionRests) return InBed.Condition;
            // To be tended: always while losing blood or for something serious; for the rest (what tending decides and small
            // things) only while someone is free to do it, and the doctor takes what tending decides first.
            if (p.needsTending && (p.bleeding || p.seriousToTend || p.doctorFree)) return InBed.Tending;
            // The player's order and the council's decree keep whatever rest the game would give.
            if (p.ordered && p.gameRest) return InBed.Ordered;
            if (p.decree && p.gameRest) return InBed.Decree;
            // Another mod's reason for rest, not one the rule covers: left as it is.
            if (p.gameRest && !p.needsTending && !p.gameOwnReasons) return InBed.GameOwn;
            return InBed.No;
        }

        /// <summary>The answer to "should they seek medical rest": in bed for any reason.</summary>
        public static bool Rests(Patient p) => Decide(p) != InBed.No;
    }
}
