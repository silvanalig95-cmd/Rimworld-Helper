using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Worship of the gods (docs/design/gods.md, stage 2 "Worship"; brainstorm items 10, 11, 13, 14, 15, 16 and 20): the
    /// public face of the worship part and the passes it runs in. The region's hour (World/Region/RegionBook.cs, beside the
    /// pantheon's) looks for hard moments to pray in, sends dedications and offerings on their way, takes offerings at the
    /// step of how real the gods are, settles prayers, keeps vows and demands, and keeps the rain away where a god said so;
    /// the region's day prunes and rolls the day's offerings and vows. It calls only the core's <see cref="Gods"/> and its
    /// two seams (<see cref="GodPresenceSeam"/>, <see cref="GodLookSeam"/>): a god's sign goes to the presence part, and while
    /// that isn't there, to a plain card worded for the step (<see cref="Sign"/>).
    /// </summary>
    public static class Worship
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The gods and their worship are on (the settings "Gods" and "Worship of the gods").</summary>
        public static bool On => Gods.On && (S?.godWorship ?? true);

        public static WorshipBook Book => On ? WorshipBook.Instance : null;

        internal static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= The passes =================

        /// <summary>The region's hour (after the pantheon's own).</summary>
        public static void Hourly(int now)
        {
            var b = Book;
            if (b == null) return;
            foreach (var map in Homes.All.ToList())
            {
                if (map == null || !map.IsPlayerHome) continue;
                Safe("dedications", () => Shrines.Hourly(b, map, now));
                Safe("prayers", () => Prayers.Hourly(b, map, now));
                Safe("offerings", () => Offerings.Hourly(b, map, now));
                Safe("vows", () => Vows.Hourly(b, map, now));
                Safe("the rain kept away", () => WorshipCouncil.Hourly(b, map, now));
                Safe("scoffers and couples", () => FaithHeart.Hourly(b, map, now));
            }
        }

        /// <summary>The region's day (after the pantheon's own).</summary>
        public static void Daily(int now)
        {
            var b = Book;
            if (b == null) return;
            foreach (var map in Homes.All.ToList())
            {
                if (map == null || !map.IsPlayerHome) continue;
                Safe("the day's offerings", () => Offerings.Daily(b, map, now));
                Safe("the day's vows", () => Vows.Daily(b, map, now));
                Safe("the Hunger's due", () => Prayers.HungerDue(b, map, now));
            }
            Safe("demands", () => GodDreams.Daily(b, now));
            Safe("pruning", () => b.Prune(now));
        }

        internal static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: " + what, e);
            }
        }

        // ================= Who =================

        /// <summary>
        /// The god someone prays to and swears by: the one they turned to after a crisis of faith, else the core's choice
        /// (Gods.SwearBy), of the moment's domain where they have one.
        /// </summary>
        public static God GodOf(Pawn p, GodDomain about = GodDomain.None)
        {
            if (p == null || !Gods.On) return null;
            var b = Book;
            int turned = b?.TurnedTo(p) ?? -1;
            if (turned >= 0)
            {
                var t = Gods.ById(turned);
                if (t != null && t.kind != GodKind.Dead) return t;
            }
            return Gods.SwearBy(p, about);
        }

        /// <summary>One of the colony's own people who can worship: a free colonist (or the colony's slave), grown or a child, with an inner life.</summary>
        public static bool Ours(Pawn p) => p != null && !p.Dead && (p.IsFreeColonist || p.IsSlaveOfColony) && !p.IsQuestLodger()
                                           && p.RaceProps?.Humanlike == true && !p.DevelopmentalStage.Baby() && People.HasInnerLife(p);

        /// <summary>
        /// Devout: with Ideology, the game's certainty from devout up; without it, the priest, or one set in their ways or of
        /// a hopeful bent (their character), and never a scoffer.
        /// </summary>
        public static bool Devout(Pawn p)
        {
            if (p == null) return false;
            if (Faiths.On && Faiths.Of(p) != null) return Faiths.Devout(p);
            if (IsPriest(p)) return true;
            if (Scoffer(p)) return false;
            return Persona.Get(p, PersonaAxis.Openness) <= -25f || Persona.Get(p, PersonaAxis.Outlook) >= 50f;
        }

        public static bool Wavering(Pawn p) => p != null && Faiths.On && Faiths.Wavering(p);

        /// <summary>A scoffer (WorshipMath.Scoffer): their faith despises religion, has no gods, or they are far from sure of it.</summary>
        public static bool Scoffer(Pawn p)
        {
            if (p == null) return false;
            bool ideology = Faiths.On && Faiths.Of(p) != null;
            bool godless = ideology && Gods.OfIdeo(Faiths.Of(p)).Count == 0;
            return WorshipMath.Scoffer(Beliefs.Forbids(p, Belief.NoReligion), ideology, godless, ideology ? Faiths.Certainty(p) : 1f, p.thingIDNumber);
        }

        /// <summary>A psychic gift: the game's psychic sensitivity well above the usual, or a RimWorld of Magic caster.</summary>
        public static bool Psychic(Pawn p)
        {
            if (p == null) return false;
            try
            {
                if (p.GetStatValue(StatDefOf.PsychicSensitivity) >= 1.3f) return true;
            }
            catch
            {
            }
            return PeopleTags.Matches(p, "mage");
        }

        // ================= The priest =================

        private static OfficeDef priestDef;
        private static bool priestLooked;

        /// <summary>The priest's seat (Defs/GodDefs/Rimfolk_Worship_Office.xml), or null.</summary>
        public static OfficeDef PriestOffice
        {
            get
            {
                if (!priestLooked)
                {
                    priestDef = DefDatabase<OfficeDef>.GetNamedSilentFail(PriestDefName);
                    priestLooked = true;
                }
                return priestDef;
            }
        }

        public const string PriestDefName = "Rimfolk_Office_Priest", ChaplainDefName = "Council_Office_Chaplain";

        /// <summary>
        /// The priest's seat is open: the gods and their worship on, a god the colony holds, and no chaplain's seat (with
        /// Ideology and a faith of the colony's own, the chaplain is the colony's priest).
        /// </summary>
        public static bool PriestSeatOpen
        {
            get
            {
                if (!On) return false;
                var chaplain = DefDatabase<OfficeDef>.GetNamedSilentFail(ChaplainDefName);
                if (chaplain != null && chaplain.Available) return false;
                var seat = Homes.Seat;
                return Gods.ColonyGods(seat).Count > 0 || Gods.Patron(seat) != null;
            }
        }

        /// <summary>The colony's priest at this home: its chaplain where the colony has one, else the priest; null without either.</summary>
        public static Pawn Priest(Map map)
        {
            if (!On) return null;
            var chaplain = Interplay.Officer(ChaplainDefName, map);
            if (chaplain != null && !chaplain.Dead) return chaplain;
            var priest = Interplay.Officer(PriestDefName, map);
            return priest != null && !priest.Dead ? priest : null;
        }

        public static bool IsPriest(Pawn p) => p != null && (Interplay.Holds(p, PriestDefName) || Interplay.Holds(p, ChaplainDefName));

        /// <summary>A RimWorld of Magic priest (its Priest, Paladin or Brightmage): their rites glow with their own light.</summary>
        public static bool MagicPriest(Pawn p) => p != null && PeopleTags.Matches(p, "trait:Priest|trait:Paladin|trait:TM_Brightmage");

        // ================= The god honoured this year =================

        /// <summary>The god the council honours this year at this home (WorshipCouncil), or null.</summary>
        public static God Honoured(Map map)
        {
            var b = Book;
            if (b == null) return null;
            var m = b.Mark(Gods.HomeKey(map), "honoured");
            if (m == null || m.value != Year(map)) return null;
            return Gods.ById(m.god);
        }

        public static bool IsHonoured(God g, Map map) => g != null && Honoured(map) == g;

        internal static int Year(Map map)
        {
            try
            {
                return map != null ? GenLocalDate.Year(map) : GenDate.Year(Find.TickManager.TicksAbs, 0f);
            }
            catch
            {
                return 0;
            }
        }

        // ================= Words =================

        /// <summary>The step's ending for a key (CC_Wsh_..._Believed, _Felt, _Walking).</summary>
        public static string Step => GodsMath.Voice(Gods.Realness);

        /// <summary>What a dedicated place is called in the colony's age: a shrine, a chapel, a church, a terminal.</summary>
        public static string ShrineWord => ("CC_Wsh_Shrine_" + WorshipMath.ShrineWord((int)Ages.Ours)).Translate();

        /// <summary>A god's place in words: "Ceren's chapel".</summary>
        public static string ShrineOf(God g) => "CC_Wsh_ShrineOf".Translate(Gods.Name(g).Named("GOD"), ShrineWord.Named("SHRINE")).Resolve();

        /// <summary>A line from a pool for someone and their god ({GOD}, {GODEPITHET}, {GODOF}, {PAWN}, {OTHER}), tagged with the god's domain and kind, the step and the extra tags; null when nothing fits.</summary>
        public static string Line(string pool, Pawn p, God g, Pawn other = null, params string[] extra) => LineWith(pool, p, g, other, null, extra);

        /// <summary>A line as <see cref="Line"/>, with more of its own values ({SUBJECT}, {NAME}...).</summary>
        public static string LineWith(string pool, Pawn p, God g, Pawn other, NamedArgument[] more, params string[] extra)
        {
            if (p == null) return null;
            try
            {
                var tags = new List<string>();
                if (g != null)
                {
                    tags.AddRange(GodSpeech.Tags(g));
                    // The god itself, for a hand-made god's own lines ([godkey:auren]): its patron's or its own name.
                    tags.Add("godkey:" + (g.patron ?? g.name ?? "").ToLowerInvariant());
                }
                tags.Add("step:" + Step.ToLowerInvariant());
                if (extra != null) tags.AddRange(extra.Where(t => !t.NullOrEmpty()));
                if (IsPriest(p)) tags.Add("priest");
                if (Scoffer(p)) tags.Add("scoffer");
                string line = Flavor.FromPool(pool, p, null, null, tags.ToArray());
                if (line.NullOrEmpty()) return null;
                var args = new List<NamedArgument> { p.Named("PAWN"), (other ?? p).Named("OTHER") };
                args.AddRange(Gods.Args(g));
                if (g == null) args.AddRange(new[] { "".Named(GodsMath.God), "".Named(GodsMath.Epithet), "".Named(GodsMath.Of) });
                if (more != null) args.AddRange(more);
                return line.Formatted(args.ToArray()).Resolve().CapitalizeFirst();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a line from " + pool, e);
                return null;
            }
        }

        /// <summary>Says a line from a pool over their head (one sentence, Speech.Say), to <paramref name="to"/> when given.</summary>
        public static bool Say(Pawn p, string pool, God g, Pawn to = null, params string[] extra)
        {
            // together: Speech.Say shows a line said to someone only when the two are really together (Together.Can).
            string line = Line(pool, p, g, to, extra);
            return !line.NullOrEmpty() && Speech.Say(p, line, to);
        }

        // ================= A god's sign =================

        /// <summary>
        /// A god's sign at a place: the presence part plays its moment (GodPresenceSeam), and while that isn't there, a plain
        /// card or a line in the news worded for the step of how real the gods are (the caller's text already is), with a
        /// glow at the place in Felt and Walking. Remembered among the god's acts either way.
        /// </summary>
        public static void Sign(God g, Map map, IntVec3 cell, PresenceKind kind, string title, string text, LookTargets look, bool letter, GodActKind act = GodActKind.Act)
        {
            if (g == null || text.NullOrEmpty()) return;
            try
            {
                var spec = PresenceSpec.For(g, map, kind, cell);
                spec.title = title;
                spec.text = text;
                if (!GodPresenceSeam.Call(spec))
                {
                    Glow(map, cell, g, kind == PresenceKind.Wrath);
                    if (letter) Tell.Letter(NewsKind.Colony, Importance.Notable, map, title, text, LetterDefOf.NeutralEvent, look);
                    else Tell.Minor(NewsKind.Colony, Importance.Notable, map, text, look);
                }
                Gods.Remember(new GodAct { god = g.id, home = Gods.HomeKey(map), kind = act, text = text, cell = cell });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a god's sign", e);
            }
        }

        private static readonly string[] HolyMotes = { "Mote_Holy", "Mote_GlowingRune", "Mote_Twinkle" };
        private static readonly string[] DarkMotes = { "TM_Mote_ShadowCircleIn", "Mote_BloodCircle" };

        /// <summary>
        /// A small light at a place where a god acted (never in Believed): A RimWorld of Magic's holy light and glowing runes
        /// where it's loaded (softly, by name), else the game's own glow; its shadow for wrath.
        /// </summary>
        public static void Glow(Map map, IntVec3 cell, God g, bool dark = false)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map) || Gods.Realness == GodRealness.Believed) return;
            try
            {
                float scale = Gods.Realness == GodRealness.Walking ? 2.2f : 1.2f;
                foreach (var name in dark ? DarkMotes : HolyMotes)
                {
                    var mote = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                    if (mote == null) continue;
                    MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, mote, scale);
                    return;
                }
                FleckMaker.Static(cell, map, dark ? FleckDefOf.PsycastAreaEffect : FleckDefOf.Meditating, scale);
                if (Gods.Realness == GodRealness.Walking) FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), map, 1.5f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a glow", e);
            }
        }

        /// <summary>A RimWorld of Magic priest's rite: their own holy light over the place (nothing without the mod or such a priest).</summary>
        public static void PriestLight(Pawn p, IntVec3 cell)
        {
            if (!MagicPriest(p) || p.Map == null || !cell.IsValid) return;
            try
            {
                foreach (var name in new[] { "Mote_MagicCircleOuter", "Mote_Holy" })
                {
                    var mote = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                    if (mote != null) MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), p.Map, mote, 1.6f);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a priest's light", e);
            }
        }

        // ================= Favour, with the year's honour =================

        /// <summary>Favour from worship (an offering, a rite), a quarter more for the god the council honours this year.</summary>
        public static void Favour(God g, Map map, float amount, string why, FavourSource source, GodOffence offence = GodOffence.None)
        {
            if (g == null || amount == 0f) return;
            if (amount > 0f && IsHonoured(g, map) && source != FavourSource.Offering) amount *= WorshipMath.HonourFactor;
            Gods.AddFavour(g, map, amount, why, source, offence);
        }
    }
}
