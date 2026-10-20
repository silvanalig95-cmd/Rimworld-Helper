using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What the marks keep watching for one person: the fights they were in lately, their addictions, their last breakdown.</summary>
    public class MarkWatch : IExposable
    {
        public int id;
        /// <summary>When they fought in a fight the colony won (the last <see cref="MaxFights"/>), for "raid after raid".</summary>
        public List<int> fights = new List<int>();
        /// <summary>The addictions they had at the last daily look (HediffDef names), to see one overcome.</summary>
        public List<string> addictions = new List<string>();
        /// <summary>Their last breakdown: when (-1: none waiting), whether it hurt someone, and the day it was.</summary>
        public int breakTick = -1;
        public bool breakAggro;
        public string breakLabel;

        public const int MaxFights = 8;

        public bool Empty => fights.Count == 0 && addictions.Count == 0 && breakTick < 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Collections.Look(ref fights, "fights", LookMode.Value);
            Scribe_Collections.Look(ref addictions, "addictions", LookMode.Value);
            Scribe_Values.Look(ref breakTick, "breakTick", -1);
            Scribe_Values.Look(ref breakAggro, "breakAggro");
            Scribe_Values.Look(ref breakLabel, "breakLabel");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                fights ??= new List<int>();
                addictions ??= new List<string>();
            }
        }
    }

    /// <summary>
    /// The little the marks need to remember between happenings (Persona/Marks.cs): the one-time marks already given (a
    /// skill mastered, a first child), what each person is watched for, and the fight going on. Pruned of people the
    /// game no longer knows when it saves and once a day.
    /// </summary>
    public class MarksTracker : GameComponent
    {
        /// <summary>"id|what" for marks that come once in a life (a skill mastered).</summary>
        public HashSet<string> once = new HashSet<string>();
        public List<MarkWatch> watches = new List<MarkWatch>();
        /// <summary>The last time a fight was seen at home, and what it's called ("the raid on the 5th of Aprimay").</summary>
        public int lastFight = -999999;
        public string fightLabel;

        public const int MaxOnce = 800;

        [Unsaved] private Dictionary<int, MarkWatch> byId;

        private static MarksTracker cached;
        private static Game cachedGame;

        public static MarksTracker Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<MarksTracker>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public MarksTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            byId = null;
            PersonaLean.Reset();
        }

        public MarkWatch Watch(Pawn p, bool create)
        {
            if (p == null) return null;
            if (byId == null)
            {
                byId = new Dictionary<int, MarkWatch>();
                foreach (var w in watches) byId[w.id] = w;
            }
            if (byId.TryGetValue(p.thingIDNumber, out var found) || !create) return found;
            found = new MarkWatch { id = p.thingIDNumber };
            watches.Add(found);
            byId[found.id] = found;
            return found;
        }

        public static string OnceKey(Pawn p, string what) => p.thingIDNumber + "|" + what;

        /// <summary>Lets go of people the game no longer knows, fights older than a year and breakdowns long past.</summary>
        public void Prune()
        {
            var known = CouncilGameComponent.KnownPawnNumbers();
            int now = Find.TickManager.TicksGame;
            foreach (var w in watches)
            {
                w.fights.RemoveAll(t => now - t > MarksMath.DayTicks * 60);
                if (w.breakTick >= 0 && now - w.breakTick > MarksMath.DayTicks * 5) w.breakTick = -1;
            }
            watches.RemoveAll(w => !known.Contains(w.id) || w.Empty);
            once.RemoveWhere(k =>
            {
                int bar = k.IndexOf('|');
                return bar <= 0 || !int.TryParse(k.Substring(0, bar), out int id) || !known.Contains(id);
            });
            if (once.Count > MaxOnce) once.Clear();
            byId = null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                try
                {
                    Prune();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("marks clean-up before saving", e);
                }
            }
            Scribe_Collections.Look(ref once, "once", LookMode.Value);
            Scribe_Collections.Look(ref watches, "watches", LookMode.Deep);
            Scribe_Values.Look(ref lastFight, "lastFight", -999999);
            Scribe_Values.Look(ref fightLabel, "fightLabel");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                once ??= new HashSet<string>();
                watches ??= new List<MarkWatch>();
                watches.RemoveAll(w => w == null);
                byId = null;
            }
        }
    }

    /// <summary>
    /// What happens to people changes them (<see cref="PersonaMarkDef"/>): a raid, a death, a kindness, a betrayal, a
    /// seat won, a child grown. A happening names a family of marks; the person's nature picks the mark (the bold
    /// harden where the timid are shaken) and scales it; the limits keep marks rare enough to mean something (one
    /// small mark per few days, a turning point never twice within its keeping, one happening one mark). A turning
    /// point is written in their journal in their own voice, and may be news. Everything comes in through hooks the
    /// game and Rimfolk already have: memories as they're gained (Patch_GoodTimes), the colony's reactions to fights,
    /// births and breakdowns (WorldReactions), elections (Offices), lost bonds (SocialWeb), the sick pulled through
    /// (HealthTracker), fine work (ArcHooks), vendettas, and a daily look (<see cref="Daily"/>).
    /// </summary>
    public static class PersonaMarks
    {
        private static MarksTracker Tracker => MarksTracker.Instance;

        public static bool On => Persona.On && Current.ProgramState == ProgramState.Playing && Find.TickManager != null && PersonaTracker.Instance != null;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Someone a happening can change: one of the colony's people with an inner life, past babyhood.</summary>
        public static bool CanBeMarked(Pawn p) =>
            p != null && !p.Dead && Persona.Keeps(p) && People.HasInnerLife(p) && !p.DevelopmentalStage.Baby() && !p.IsQuestLodger();

        // ---------------- Giving a mark ----------------

        /// <summary>
        /// A happening of this family touched them. The first mark of the family their nature fits is given, scaled by
        /// their nature, unless the limits say not now. Returns the mark, or null.
        /// </summary>
        public static PersonaMark Give(Pawn p, string family, Pawn other = null, string label = null, float scale = 1f)
        {
            if (!On || family == null || !CanBeMarked(p) || other == p) return null;
            var list = MarkDefs.Family(family);
            if (list == null || list.Count == 0) return null;
            var values = Persona.Values(p);
            bool child = p.DevelopmentalStage.Juvenile();
            PersonaMarkDef pick = null;
            PersonaMarkExtension ext = null;
            foreach (var d in list)
            {
                var x = MarkDefs.Ext(d);
                if (x == null || (x.ForChildren && !child) || (x.ForAdults && child) || !MarksMath.Holds(x.Conditions, values)) continue;
                pick = d;
                ext = x;
                break;
            }
            if (pick == null) return null;
            if (ext.chance < 1f && !Rand.Chance(ext.chance)) return null;
            string finalLabel = label ?? pick.defaultLabel ?? "";
            int now = Now;
            var next = new MarkRecord
            {
                def = pick.defName, story = MarkDefs.StoryOf(pick), about = About(other?.thingIDNumber ?? -1, finalLabel), major = pick.major, tick = now
            };
            if (MarksMath.Blocked(History(p), next, RepeatDays(pick, ext), now) != null) return null;
            var mark = Persona.Shape(p, pick, other, finalLabel, scale * MarksMath.Scale(ext.Leans, values));
            if (mark == null) return null;
            try
            {
                Tell(p, pick, ext, other, mark);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona mark told: " + pick.defName, e);
            }
            return mark;
        }

        private static int RepeatDays(PersonaMarkDef def, PersonaMarkExtension x)
        {
            if (x.repeatDays > 0) return x.repeatDays;
            if (!def.major) return MarksMath.MinorRepeatDays;
            return def.keepDays > 0 ? def.keepDays : 100000;
        }

        private static string About(int otherId, string label) => otherId >= 0 ? "#" + otherId : label ?? "";

        private static List<MarkRecord> History(Pawn p)
        {
            var list = new List<MarkRecord>();
            var r = Persona.Of(p);
            if (r == null) return list;
            foreach (var m in r.marks)
            {
                if (m?.def == null) continue;
                list.Add(new MarkRecord
                {
                    def = m.def.defName, story = MarkDefs.StoryOf(m.def), about = About(m.other, m.label), major = m.def.major, tick = m.tick
                });
            }
            return list;
        }

        /// <summary>Whether they carry a mark of this story from the last <paramref name="days"/> days.</summary>
        public static bool HasRecent(Pawn p, string story, float days)
        {
            var r = Persona.Of(p);
            if (r == null) return false;
            int now = Now;
            return r.marks.Any(m => m?.def != null && MarkDefs.StoryOf(m.def) == story && now - m.tick < days * MarksMath.DayTicks);
        }

        /// <summary>A turning point in their own words, and as news (and in the chronicle) where the mark says so.</summary>
        private static void Tell(Pawn p, PersonaMarkDef def, PersonaMarkExtension x, Pawn other, PersonaMark mark)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            var otherArg = other != null ? other.Named("OTHER") : (mark.otherName ?? "").Named("OTHER");
            var labelArg = (mark.label ?? "").Named("LABEL");
            // One happening, one line: not when the happening already has its line in their journal (the council's line
            // for an election, a wedding's), and a death is written about once, whoever notices it first (the grief, the
            // bond lost, the group: JournalTracker.FirstOnLoss).
            if (x.journal != null && x.journal.Any && !comp.journals.WroteWithin(p)
                && (other == null || MarkDefs.StoryOf(def) != "Loss" || comp.journals.FirstOnLoss(p, other)))
            {
                string raw = x.journal.Pick(ToneUtility.ToneFor(p), Flavor.TagsFor(p));
                if (!raw.NullOrEmpty()) comp.journals.WriteText(p, raw.Formatted(p.Named("PAWN"), otherArg, labelArg).Resolve());
            }
            if (x.news.NullOrEmpty() || !p.IsColonist) return;
            string text = x.news.Formatted(p.Named("PAWN"), otherArg, labelArg).Resolve();
            News.Post(text, new LookTargets(p), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
            if (x.chronicle) comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, text, p));
        }

        // ---------------- Labels ----------------

        private static float Longitude(Pawn p)
        {
            var map = p?.MapHeld ?? Find.AnyPlayerHomeMap;
            return map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;
        }

        /// <summary>"the 5th of Aprimay": the day a game tick fell on, as deeds and shared memories say it too.</summary>
        public static string DayLabel(int gameTick, Pawn p = null)
        {
            long abs = GenDate.TickGameToAbs(gameTick);
            return "CC_Mark_Day".Translate(GenDate.QuadrumDateStringAt(abs, Longitude(p))).Resolve();
        }

        /// <summary>
        /// "Aprimay 5501": the season and year beside a mark on the character page (the day itself is usually in the
        /// mark's own words: "since the raid on the 5th of Aprimay").
        /// </summary>
        public static string DateLabel(int gameTick, Pawn p = null)
        {
            long abs = GenDate.TickGameToAbs(gameTick);
            float lon = Longitude(p);
            return "CC_Mark_Date".Translate(GenDate.Quadrum(abs, lon).Label(), GenDate.Year(abs, lon)).Resolve();
        }

        public static string Today(Pawn p = null) => DayLabel(Now, p);

        /// <summary>A fight at home is on (or just was): what it's called, kept from its first hour.</summary>
        private static void NoteFight(Faction foe = null)
        {
            var t = Tracker;
            if (t == null) return;
            int now = Now;
            if (now - t.lastFight > GenDate.TicksPerHour * 12 || t.fightLabel.NullOrEmpty())
            {
                var social = CouncilGameComponent.Instance?.social;
                foe ??= social?.battleFaction;
                // The name the fight has everywhere else too (deeds, shared memories).
                t.fightLabel = FightNames.Of(social?.battleMap ?? Find.AnyPlayerHomeMap, foe);
            }
            t.lastFight = now;
        }

        private static string FightLabel => Tracker?.fightLabel ?? FightNames.Of(null);

        private static bool FightOn(Map map) =>
            (CouncilGameComponent.Instance?.social?.battleActive ?? false) || MapFacts.ThreatOn(map);

        /// <summary>A fight is on, or was within the last half day.</summary>
        private static bool FightLately(Pawn p)
        {
            if (FightOn(p?.MapHeld))
            {
                NoteFight();
                return true;
            }
            var t = Tracker;
            return t != null && Now - t.lastFight < GenDate.TicksPerHour * 12;
        }

        // ---------------- Memories (Patch_GoodTimes) ----------------

        /// <summary>
        /// A memory was just given to someone (every memory passes through here, so the families it brings are found in
        /// a dictionary first and nothing else is done for the rest).
        /// </summary>
        public static void Notify_Memory(Pawn pawn, Thought_Memory m)
        {
            if (m?.def == null || pawn == null || !Persona.On) return;
            var fams = MarkDefs.ForThought(m.def);
            if (fams == null) return;
            try
            {
                if (!On || m.pawn != pawn || !Gained(pawn, m) || !CanBeMarked(pawn)) return;
                foreach (var fam in fams) FromMemory(pawn, m, fam);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona marks from a memory", e);
            }
        }

        /// <summary>The memory is on them now (added, or renewing one just like it): not refused by their traits.</summary>
        private static bool Gained(Pawn p, Thought_Memory m)
        {
            var mem = p.needs?.mood?.thoughts?.memories?.Memories;
            if (mem == null) return false;
            for (int i = 0; i < mem.Count; i++)
            {
                var x = mem[i];
                if (x == m || (x != null && x.def == m.def && x.age == 0 && x.otherPawn == m.otherPawn)) return true;
            }
            return false;
        }

        private static void FromMemory(Pawn p, Thought_Memory m, string fam)
        {
            var other = m.otherPawn;
            var first = MarkDefs.Family(fam)?.FirstOrDefault();
            var x = MarkDefs.Ext(first);
            if (x == null) return;
            if (fam == "LostPet")
            {
                // The animal is named in the label: {OTHER} is for people.
                Give(p, fam, null, other?.LabelShort ?? first.defaultLabel);
                return;
            }
            if (other != null && (other.RaceProps == null || !other.RaceProps.Humanlike)) other = null;
            if (x.needsOther && other == null) return;
            if (other != null && x.minOpinion > -101 && (p.relations?.OpinionOf(other) ?? 0) < x.minOpinion) return;
            switch (fam)
            {
                case "SavedBy":
                    // Carried out of a fight while downed; a rescue from a faint or a fever isn't the same thing.
                    if (other == null || !FightLately(p)) return;
                    Give(p, fam, other, FightLabel);
                    Give(other, "SavedSomeone", p, FightLabel);
                    return;
                case "SecretBetrayed":
                    Give(p, fam, other);
                    if (other != null) Give(other, "BetrayedSomeone", p);
                    return;
                case "ForgivenBy":
                {
                    // A kind word soon after a breakdown that hurt someone: forgiven.
                    var w = Tracker?.Watch(p, false);
                    if (other == null || w == null || w.breakTick < 0 || !w.breakAggro || Now - w.breakTick > MarksMath.DayTicks * 4) return;
                    if (Give(p, fam, other, w.breakLabel) != null) w.breakTick = -1;
                    return;
                }
                default:
                    Give(p, fam, other, x.labelDay ? Today(p) : null);
                    return;
            }
        }

        // ---------------- The colony's own happenings ----------------

        /// <summary>Lying downed while a fight is on at home (QuirkTracker's hourly look at the downed).</summary>
        public static void Notify_Downed(Pawn p)
        {
            try
            {
                if (!On || !CanBeMarked(p) || !FightOn(p.MapHeld)) return;
                NoteFight();
                Give(p, "Downed", null, FightLabel);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona marks: downed", e);
            }
        }

        /// <summary>
        /// A fight at home was won (WorldReactions.Victory): who stood out held the line, those who fought through a
        /// costly one survived it, and raid after raid wears on people or hardens them.
        /// </summary>
        public static void Notify_Battle(List<Pawn> fighters, Faction foe, int kills, List<string> fallen, Dictionary<Pawn, int> killsBy)
        {
            try
            {
                if (!On || fighters.NullOrEmpty() || Tracker == null) return;
                // A real fight, as the colony counts one (WorldReactions.Victory).
                if (kills < 3 && !(foe != null && kills >= 1)) return;
                NoteFight(foe);
                string label = FightLabel;
                int now = Now;
                var people = fighters.Where(CanBeMarked).Distinct().ToList();
                foreach (var f in people)
                {
                    var w = Tracker.Watch(f, true);
                    if (w.fights.Count == 0 || now - w.fights[w.fights.Count - 1] > GenDate.TicksPerHour * 12) w.fights.Add(now);
                    while (w.fights.Count > MarkWatch.MaxFights) w.fights.RemoveAt(0);
                }
                var hero = killsBy?.Where(kv => kv.Key != null && kv.Value >= 2 && people.Contains(kv.Key)).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).FirstOrDefault();
                if (hero != null) Give(hero, "HeldTheLine", null, label);
                bool costly = fallen != null && fallen.Count > 0;
                foreach (var f in people)
                {
                    if (costly && f != hero && !HasRecent(f, "Loss", 1f)) Give(f, "SurvivedLoss", null, label);
                    if (MarksMath.CountWithin(Tracker.Watch(f, true).fights, now, 60f) >= 4) Give(f, "ManyRaids", null, null);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona marks: a fight won", e);
            }
        }

        /// <summary>A breakdown from their mood (WorldReactions.Breakdown): in a fight it's breaking under fire; a violent one hurt someone.</summary>
        public static void Notify_Breakdown(Pawn p, MentalStateDef state)
        {
            try
            {
                if (!On || state == null || !CanBeMarked(p) || Tracker == null) return;
                var w = Tracker.Watch(p, true);
                w.breakTick = Now;
                w.breakAggro = state.IsAggro;
                w.breakLabel = Today(p);
                if (FightOn(p.MapHeld))
                {
                    NoteFight();
                    Give(p, "BrokeUnderFire", null, FightLabel);
                }
                else if (state.IsAggro) Give(p, "LashedOut", null, w.breakLabel);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona marks: breakdown", e);
            }
        }

        /// <summary>A child born to colonists (WorldReactions.Birth): their first makes them parents.</summary>
        public static void Notify_Birth(Pawn baby, Pawn mother, Pawn father)
        {
            try
            {
                if (!On || baby == null) return;
                foreach (var parent in new[] { mother, father })
                    if (parent != null && parent != baby && CanBeMarked(parent) && (parent.relations?.ChildrenCount ?? 0) <= 1)
                        Give(parent, "BecameParent", baby, baby.LabelShort);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona marks: birth", e);
            }
        }

        /// <summary>Voted into a seat (Offices.Hold); <paramref name="again"/> when they held it already (that's no new turning point).</summary>
        public static void Notify_Elected(Pawn winner, string office, bool again)
        {
            try
            {
                if (!again) Give(winner, "Elected", null, office);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona marks: election", e);
            }
        }

        /// <summary>A seat left empty (Offices.Daily): when the speaker died, the colony has lost its leader.</summary>
        public static void Notify_SeatEmptied(Pawn gone, string officeDefName)
        {
            try
            {
                if (!On || gone == null || !gone.Dead || officeDefName != "Council_Office_Speaker") return;
                foreach (var p in PawnsFinder.AllMaps_FreeColonists.ToList())
                    if (p != gone && CanBeMarked(p) && (p.relations?.OpinionOf(gone) ?? 0) >= 0)
                        Give(p, "LostLeader", gone);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona marks: leader lost", e);
            }
        }

        /// <summary>
        /// Someone they were bonded to died (SocialWeb.Daily): a friend or a comrade is a friend lost, even when the game's
        /// own memory of it didn't come (the memories usually tell it first, and one death leaves one mark), and so is
        /// a teacher, for their student.
        /// </summary>
        public static void Notify_BondLost(Pawn alive, Pawn dead, TieKind kind, bool wasStudent)
        {
            try
            {
                if (kind == TieKind.Friends || kind == TieKind.Comrades || (kind == TieKind.Mentor && wasStudent)) Give(alive, "LostFriend", dead);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona marks: bond lost", e);
            }
        }

        /// <summary>A doctor pulled them through a dangerous illness (HealthTracker.PulledThrough).</summary>
        public static void Notify_PulledThrough(Pawn doctor, Pawn patient, string illness)
        {
            try
            {
                if (doctor == null || doctor == patient) return;
                Give(patient, "NursedBack", doctor, illness.NullOrEmpty() ? null : illness);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona marks: pulled through", e);
            }
        }

        /// <summary>Something they made came out a masterwork or legendary (Patch_ArcMade).</summary>
        public static void Notify_Made(Pawn worker, Thing thing)
        {
            try
            {
                if (!On || thing == null || !thing.TryGetQuality(out var q) || q < QualityCategory.Masterwork) return;
                Give(worker, q == QualityCategory.Legendary ? "Legendary" : "Masterwork", null, thing.def.label);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona marks: made", e);
            }
        }

        /// <summary>They swore vengeance on a people for someone (Vendettas).</summary>
        public static void Notify_Sworn(Pawn avenger, Pawn lostOne, string people)
        {
            try
            {
                Give(avenger, "SworeVengeance", lostOne, people);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("persona marks: vengeance sworn", e);
            }
        }

        // ---------------- Once a day ----------------

        /// <summary>
        /// The slow things, once a day: a long illness, a hungry season, an addiction overcome, a skill mastered, a new
        /// faith, a long peace (CouncilDynamics, the second half of the council's day).
        /// </summary>
        public static void Daily(CouncilGameComponent comp)
        {
            var t = Tracker;
            if (t == null) return;
            t.Prune();
            if (!On) return;
            int now = Now;
            foreach (var p in PawnsFinder.AllMaps_FreeColonists.ToList())
            {
                if (!CanBeMarked(p)) continue;
                try
                {
                    DailyFor(t, p, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("persona marks: daily", e);
                }
            }
        }

        private static void DailyFor(MarksTracker t, Pawn p, int now)
        {
            var hediffs = p.health?.hediffSet?.hediffs;
            if (hediffs != null)
            {
                // A long illness: sick with something the game calls an illness for eight days or more.
                var ill = hediffs.FirstOrDefault(h => h.def.makesSickThought && h.Visible && h.ageTicks >= MarksMath.DayTicks * 8);
                if (ill != null) Give(p, "LongIllness", null, ill.def.label);
                // A hungry season: malnutrition gone past the first stage.
                var hunger = hediffs.FirstOrDefault(h => h.def == HediffDefOf.Malnutrition && h.Severity >= 0.3f);
                if (hunger != null) Give(p, "HungrySeason", null, SeasonLabel(p));
                // An addiction overcome: one they had is gone, and they're still here.
                var now_ = hediffs.OfType<Hediff_Addiction>().Select(h => h.def.defName).Distinct().ToList();
                var w = t.Watch(p, now_.Count > 0);
                if (w != null)
                {
                    foreach (var gone in w.addictions.Where(a => !now_.Contains(a)).ToList())
                    {
                        var def = DefDatabase<HediffDef>.GetNamedSilentFail(gone);
                        Give(p, "AddictionOvercome", null, def?.label ?? gone);
                    }
                    w.addictions = now_;
                }
            }
            // A skill mastered: level 20 in a skill they care about, once per skill.
            var skills = p.skills?.skills;
            if (skills != null)
                foreach (var s in skills)
                {
                    if (s.TotallyDisabled || s.Level < SkillRecord.MaxLevel || !People.Passionate(s)) continue;
                    string key = MarksTracker.OnceKey(p, "master:" + s.def.defName);
                    if (t.once.Contains(key)) continue;
                    if (Give(p, "Mastered", null, s.def.label) != null) t.once.Add(key);
                }
            // A new faith: they changed their beliefs in the last day and a half, after some time among us.
            if (ModsConfig.IdeologyActive && p.ideo?.Ideo != null && p.ideo.PreviousIdeos != null && p.ideo.PreviousIdeos.Count > 0
                && p.ideo.joinTick > 0 && now - p.ideo.joinTick < MarksMath.DayTicks * 1.5f
                && (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) > MarksMath.DayTicks * 3)
                Give(p, "Converted", null, p.ideo.Ideo.name);
            // A long peace: a year without a big threat at home, lived here all through it.
            var map = p.MapHeld;
            if (map != null && map.IsPlayerHome && (p.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) >= MarksMath.DayTicks * 60)
            {
                int last = Math.Max(map.storyState?.LastThreatBigTick ?? 0, 0);
                if (now - last >= MarksMath.DayTicks * 60) Give(p, "LongPeace", null, null);
            }
        }

        private static string SeasonLabel(Pawn p)
        {
            var map = p.MapHeld ?? Find.AnyPlayerHomeMap;
            if (map == null) return "CC_MarkLabel_HungrySeason".Translate("", "").Resolve();
            long abs = Find.TickManager.TicksAbs;
            var ll = Find.WorldGrid.LongLatOf(map.Tile);
            var season = GenDate.Season(abs, ll);
            return "CC_MarkLabel_HungrySeason".Translate(season.Label(), GenDate.Year(abs, ll.x)).Resolve();
        }
    }
}
