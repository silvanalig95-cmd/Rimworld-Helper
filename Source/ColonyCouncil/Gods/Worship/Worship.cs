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
    /// pantheon's) looks for hard moments to pray in, sends dedications and offerings on their way, takes offerings (in the
    /// night, or plainly when the god is drawn near: GodNear), settles prayers, keeps vows and demands, and keeps the rain
    /// away where a god said so;
    /// the region's day prunes and rolls the day's offerings and vows. It calls only the core's <see cref="Gods"/> and its
    /// two seams (<see cref="GodPresenceSeam"/>, <see cref="GodLookSeam"/>): a god's sign goes to the presence part, and while
    /// that isn't there, to a plain card worded as plainly as the god acted (<see cref="Sign"/>).
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

        /// <summary>A scoffer (WorshipMath.Scoffer): their faith despises religion, they are far from sure of it, or it's who they are.</summary>
        public static bool Scoffer(Pawn p)
        {
            if (p == null) return false;
            bool ideology = Faiths.On && Faiths.Of(p) != null;
            return WorshipMath.Scoffer(Beliefs.Forbids(p, Belief.NoReligion), ideology, ideology ? Faiths.Certainty(p) : 1f, p.thingIDNumber);
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
        /// The priest's seat is open: the gods and their worship on, a god someone of the colony prays to, and no chaplain's
        /// seat (with Ideology and a faith of the colony's own, the chaplain is the colony's priest, as a role only).
        /// </summary>
        public static bool PriestSeatOpen
        {
            get
            {
                if (!On) return false;
                var chaplain = DefDatabase<OfficeDef>.GetNamedSilentFail(ChaplainDefName);
                if (chaplain != null && chaplain.Available) return false;
                return PrayedTo(Homes.Seat).Count > 0;
            }
        }

        /// <summary>
        /// The gods the colony's people pray to at a home (each one's god, Worship.GodOf), the most prayed-to first, and those
        /// with a place dedicated to them there; never a dead god. What the colony does, not how a god stands with it.
        /// </summary>
        public static List<God> PrayedTo(Map map)
        {
            var count = new Dictionary<God, int>();
            if (map == null || !On) return new List<God>();
            foreach (var p in Homes.People(map))
            {
                if (!Ours(p) || Scoffer(p)) continue;
                var g = GodOf(p);
                if (g == null || g.kind == GodKind.Dead) continue;
                count[g] = (count.TryGetValue(g, out int n) ? n : 0) + 1;
            }
            foreach (var s in Shrines.Of(map, null))
            {
                var g = Gods.ById(s.god);
                if (g != null && g.kind != GodKind.Dead && !count.ContainsKey(g)) count[g] = 0;
            }
            return count.Keys.OrderByDescending(g => count[g]).ThenBy(g => g.id).ToList();
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

        /// <summary>
        /// A key's ending for how plainly a god acted this time (CC_Wsh_..._Felt, _Plain): as the gods are felt, or plainly,
        /// when it was drawn near enough to show itself (GodNear.Plain).
        /// </summary>
        public static string Step(bool plain) => plain ? "Plain" : "Felt";

        /// <summary>What a dedicated place is called in the colony's age: a shrine, a chapel, a church, a terminal.</summary>
        public static string ShrineWord => ("CC_Wsh_Shrine_" + WorshipMath.ShrineWord((int)Ages.Ours)).Translate();

        /// <summary>A god's place in words: "Ceren's chapel".</summary>
        public static string ShrineOf(God g) => "CC_Wsh_ShrineOf".Translate(Called(g).Named("GOD"), ShrineWord.Named("SHRINE")).Resolve();

        /// <summary>
        /// A line from a pool for someone and their god ({GOD}, {GODEPITHET}, {GODOF}, {PAWN}, {OTHER}), tagged with the god's
        /// domain and kind, whether it is drawn near now (near, else veiled: GodNear), the speaker's doubt (doubt, for one who
        /// wavers) and the extra tags; null when nothing fits.
        /// </summary>
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
                    // The god itself, for its own lines ([godkey:smoulder]): its key (its short name).
                    tags.Add("godkey:" + (g.Key ?? g.patron ?? "").ToLowerInvariant());
                }
                tags.Add(g != null && GodNear.Near(g, p.MapHeld) ? "near" : "veiled");
                if (Wavering(p)) tags.Add("doubt");
                if (extra != null) tags.AddRange(extra.Where(t => !t.NullOrEmpty()));
                if (IsPriest(p)) tags.Add("priest");
                if (Scoffer(p)) tags.Add("scoffer");
                string line = Flavor.FromPool(pool, p, null, null, tags.ToArray());
                if (line.NullOrEmpty()) return null;
                var args = new List<NamedArgument> { p.Named("PAWN"), (other ?? p).Named("OTHER") };
                if (g != null) args.AddRange(new[] { Called(g, p).Named(GodsMath.God), (g.epithet ?? "").Named(GodsMath.Epithet), Gods.GodOf(g).Named(GodsMath.Of) });
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
        /// card or a line in the news, with a glow at the place. The caller's text says only what was seen, as plainly as the
        /// god acted this time (<paramref name="plain"/>, decided by the caller with the words it chose; else the god's own
        /// nearness now, GodNear.Plain); the god behind it is named after it only as the core allows (<see cref="Hand"/>).
        /// Remembered among the god's acts, and in the colony's book through those who saw it.
        /// </summary>
        public static void Sign(God g, Map map, IntVec3 cell, PresenceKind kind, string title, string text, LookTargets look, bool letter, GodActKind act = GodActKind.Act,
            bool? plain = null)
        {
            if (g == null || text.NullOrEmpty()) return;
            try
            {
                var spec = PresenceSpec.For(g, map, kind, cell);
                if (plain.HasValue) spec.veiled = !plain.Value;
                // Plain only where someone of the colony was there to see it; then the colony is sure, and says whose it was.
                if (!spec.veiled && !GodWorld.Witnessed(map, cell)) spec.veiled = true;
                spec.title = title;
                string seen = text;
                text += !spec.veiled && Gods.CardName(g, map) == null && Gods.Known(g)
                    ? " " + "CC_GW_Hand_Plain".Translate(Called(g).Named("GOD")).Resolve()
                    : Hand(g, map, cell);
                spec.text = text;
                if (!GodPresenceSeam.Call(spec))
                {
                    Glow(map, cell, g, kind == PresenceKind.Wrath, !spec.veiled);
                    if (letter) Tell.Letter(NewsKind.Colony, Importance.Notable, map, title, text, LetterDefOf.NeutralEvent, look);
                    else Tell.Minor(NewsKind.Colony, Importance.Notable, map, text, look);
                }
                Witness(g, map, act, seen, cell);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a god's sign", e);
            }
        }

        private static readonly string[] HolyMotes = { "Mote_Holy", "Mote_GlowingRune", "Mote_Twinkle" };
        private static readonly string[] DarkMotes = { "TM_Mote_ShadowCircleIn", "Mote_BloodCircle" };

        /// <summary>
        /// A small light at a place where a god acted: A RimWorld of Magic's holy light and glowing runes where it's loaded
        /// (softly, by name), else the game's own glow; its shadow for wrath; larger, with a flash, when the god acted plainly.
        /// </summary>
        public static void Glow(Map map, IntVec3 cell, God g, bool dark = false, bool plain = false)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map)) return;
            try
            {
                float scale = plain ? 2.2f : 1.2f;
                foreach (var name in dark ? DarkMotes : HolyMotes)
                {
                    var mote = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                    if (mote == null) continue;
                    MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, mote, scale);
                    return;
                }
                FleckMaker.Static(cell, map, dark ? FleckDefOf.PsycastAreaEffect : FleckDefOf.Meditating, scale);
                if (plain) FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(), map, 1.5f);
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

        /// <summary>
        /// Favour from worship (an offering, a rite), a quarter more for the god the council honours this year; nothing for a
        /// god worship doesn't move (Helios). Hidden state: never shown, only acted on.
        /// </summary>
        public static void Favour(God g, Map map, float amount, string why, FavourSource source, GodOffence offence = GodOffence.None)
        {
            if (g == null || amount == 0f || !GodWorshipDef.Moved(g)) return;
            if (amount > 0f && IsHonoured(g, map) && source != FavourSource.Offering) amount *= WorshipMath.HonourFactor;
            Gods.AddFavour(g, map, amount, why, source, offence);
        }

        // ================= The colony's book =================

        /// <summary>
        /// Something of a god's at a home for the gods' history (Gods.Remember): an act on the land (a sign, a blessing, a
        /// wrath) reaches the colony's book through those who saw it, as they took it; anything else (an offering left, a
        /// festival's favour, a vow) stays hidden. <paramref name="text"/> is what was seen, never why.
        /// </summary>
        public static void Witness(God g, Map map, GodActKind kind, string text, IntVec3 cell = default)
        {
            if (g == null || text.NullOrEmpty()) return;
            Gods.Remember(new GodAct { god = g.id, home = Gods.HomeKey(map), kind = kind, text = text, cell = cell == default ? IntVec3.Invalid : cell });
        }

        /// <summary>
        /// The colony's book, written by what its people saw of their own worship (a place given to a god, a festival kept, a
        /// vow kept or broken, a beast let go): those near enough to see it, and <paramref name="also"/>, under the god they
        /// gave it to, which they know because they did it themselves.
        /// </summary>
        public static void Booked(God g, Map map, IntVec3 cell, string words, Pawn also = null)
        {
            if (g == null || map == null || words.NullOrEmpty()) return;
            try
            {
                var who = Gods.Witnesses(map, cell);
                if (also != null && also.IsColonist && !who.Contains(also)) who.Insert(0, also);
                if (who.Count > 0) GodLore.Saw(who, g, words);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: the colony's book", e);
            }
        }

        /// <summary>
        /// The colony's name for a god its people pray to, give places to, vow to and keep the days of (their own doings, which
        /// they know): its page in the colony's book, else the name <paramref name="speaker"/> uses for it (GodLore.NameOf), else
        /// its own; "a god" for one the colony doesn't know. Never a claim that the god did anything: for that, <see cref="Hand"/>.
        /// </summary>
        public static string Called(God g, Pawn speaker = null)
        {
            if (g == null) return "";
            if (!Gods.Known(g)) return "CC_Wsh_UnknownGod".Translate().ToString();
            return speaker != null ? GodLore.NameOf(g, speaker) ?? Gods.Name(g) : GodLore.Instance?.PageOf(g)?.name ?? Gods.Name(g);
        }

        /// <summary>
        /// Who was behind something that happened, as a card may say it (the core's rule, Gods.CardName): plainly where the
        /// colony knows it was this god (it has seen the god act plainly, or it knows the sign), else "some say" where someone
        /// of the colony saw it (<paramref name="seen"/>, else whoever was near the cell), else nothing: what happened stays
        /// unnamed.
        /// </summary>
        public static string Hand(God g, Map map, IntVec3 cell, bool? seen = null, string sign = null)
        {
            if (g == null) return "";
            string name = Gods.CardName(g, map, sign);
            if (name != null) return " " + "CC_GW_Hand_Plain".Translate(name.Named("GOD")).Resolve();
            if (Gods.Known(g) && (seen ?? GodWorld.Witnessed(map, cell)))
                return " " + "CC_GW_Hand_Felt".Translate(Called(g).Named("GOD")).Resolve();
            return "";
        }
    }
}
