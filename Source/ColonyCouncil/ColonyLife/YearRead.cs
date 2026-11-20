using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A29: the year read aloud (the content update, the player's go of 20 November 2026). The year letter is written on the
    /// landing's anniversary (World/YearLetter.cs). That evening its writer stands at the table and reads a few lines of it
    /// aloud, three to five, one sentence each (who came and who was lost, what the colony finished, an aim kept, the names
    /// it uses, and the colony as it stands: YearReadMath.Pick), with "Answers play out on the map": at the founding day's
    /// own gathering when one is planned for that evening, else at a short gathering of its own (words heard at the table).
    /// Those there share a memory of the year read together (Rimfolk_Mem_YearRead, brought up in talk later). The colony's
    /// record keeper (the council's answer "Make A keeper of the colony's record") writes it from then on, which counts toward
    /// the Annalist's name as any writing of it does. With Vanilla Books Expanded (by name only) and a writer's table, the
    /// writer first writes it up there in the day, and it becomes a book called after the year ("The second year") that goes
    /// on the colony's shelf; with the mod's printing researched, a newspaper instead. Without the mod, the reading alone.
    /// </summary>
    public static class YearRead
    {
        /// <summary>
        /// The lines to read, worked out before the year's figures are begun again (YearLetter.Write): the year's people,
        /// works, aims, names, and as filler the colony as it stands.
        /// </summary>
        public static List<string> Lines(CouncilGameComponent comp, Map map, int since, int now, int year)
        {
            var annals = comp?.annals;
            var book = comp?.life;
            if (annals == null || book == null) return new List<string>();
            var lines = new List<string>();
            var joined = annals.joined.Distinct().Take(4).ToList();
            if (joined.Count > 0) lines.Add("CC_Life_Read_Joined".Translate(TextUtil.JoinAnd(joined).Named("NAMES")).Resolve());
            var died = annals.died.Distinct().Take(4).ToList();
            if (died.Count > 0) lines.Add("CC_Life_Read_Died".Translate(TextUtil.JoinAnd(died).Named("NAMES")).Resolve());
            var built = book.built.Distinct().Take(3).ToList();
            if (built.Count > 0) lines.Add("CC_Life_Read_Built".Translate(TextUtil.JoinAnd(built).Named("LIST")).Resolve());
            var kept = KeptAim(comp, since, now);
            if (kept != null) lines.Add(kept);
            var colonists = map?.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList() ?? new List<Pawn>();
            var named = colonists.Select(p => (p, name: comp.epithets.NameOf(p))).Where(x => !x.name.NullOrEmpty()).InRandomOrder().FirstOrDefault();
            if (named.p != null) lines.Add("CC_Life_Read_Name".Translate(named.p.Named("PAWN"), named.name.Named("NAME")).Resolve());
            var filler = new List<string>
            {
                annals.raids > 0 ? Texts.CountKey("CC_Life_Read_Raids", annals.raids).Translate(annals.raids.Named("COUNT")).Resolve() : null,
                "CC_Life_Read_Count".Translate(colonists.Count.Named("COUNT")).Resolve(),
                "CC_Life_Read_Years".Translate(HerdStory.Ordinal(year).Named("ORD")).Resolve()
            };
            return YearReadMath.Pick(lines, filler);
        }

        /// <summary>An aim said at the year's first supper and reached within the year, as a line to read; null if none.</summary>
        private static string KeptAim(CouncilGameComponent comp, int since, int now)
        {
            foreach (var a in comp.life.aims.Where(x => x.tick >= since && x.tick < now).InRandomOrder())
            {
                var p = comp.aspirations.all.FirstOrDefault(x => x.pawn != null && x.pawn.ThingID == a.pawnId && x.done && x.def?.defName == a.def && x.doneTick >= a.tick)?.pawn;
                if (p != null) return "CC_Life_Read_AimKept".Translate(p.Named("PAWN"), (a.goal ?? "").Named("GOAL")).Resolve();
            }
            return null;
        }

        /// <summary>
        /// After the letter: the reading that evening, by its writer, and (with Vanilla Books Expanded) the writing up at the
        /// writer's table during the day. Once a year.
        /// </summary>
        public static void Plan(CouncilGameComponent comp, Map map, int year, Pawn writer, List<string> lines)
        {
            var book = comp?.life;
            if (book == null || map == null || writer == null || lines == null || lines.Count == 0) return;
            if (!PlayOuts.On || !YearReadMath.Due(year, book.yearRead) || !PlayOuts.OurPerson(writer) || writer.MapHeld != map) return;
            book.yearRead = year;
            book.built.Clear();
            string ord = HerdStory.Ordinal(year);
            LifeBook.Safe("the year written up", () => YearBook.Plan(writer, map, ord));
            var tracker = PlayOutTracker.Instance;
            int now = Find.TickManager.TicksGame;
            // The founding day's own gathering this evening (the calendar's, by the day's name), if there is one: read there.
            string founding = ColonyCalendar.For(map)?.Get("founding")?.label;
            var at = founding.NullOrEmpty() ? null : tracker?.plays.FirstOrDefault(p => p.map == map && p.IsGathering && p.state != PlayState.Done
                                                                                      && now - p.created < GenDate.TicksPerDay && Readings.At(p) == null
                                                                                      && string.Equals(p.label, founding, StringComparison.OrdinalIgnoreCase));
            PlayOut plan = at;
            if (plan != null)
            {
                if (!plan.invited.Contains(writer)) plan.invited.Add(writer);
            }
            else
            {
                plan = new DilemmaEffect_Words
                {
                    label = "CC_Life_Read_Label".Translate(ord.Named("ORD")).Resolve(), organizer = PlayWho.A, target = DilemmaTarget.Colony,
                    spots = new List<PlaySpot> { PlaySpot.Dining, PlaySpot.Gather, PlaySpot.Rec }, pool = "Rimfolk_Pool_YearRead_Aside",
                    hours = new IntRange(17, 22), duration = new FloatRange(1.25f, 1.5f), waitHours = 30f
                }.Begin(new DilemmaContext { map = map, a = writer, why = "CC_Life_Read_Label".Translate(ord.Named("ORD")).Resolve() });
            }
            if (plan == null) return;
            var r = Readings.Add(plan, ReadingKind.Year);
            if (r == null) return;
            r.speaker = writer;
            r.lines = lines.ToList();
            r.label = "CC_Life_Read_Label".Translate(ord.Named("ORD")).Resolve();
            Tell.Minor(NewsKind.Colony, Importance.Notable, map, (at != null ? "CC_Life_Read_PlannedAt" : "CC_Life_Read_Planned")
                .Translate(writer.Named("PAWN"), ord.Named("ORD"), (at?.label ?? "").Named("LABEL")).Resolve(), new LookTargets(writer), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The writer reads the next line when they're at the table; false when they're not (they're waited for a while).</summary>
        public static bool ReadNext(GatheringReading r, List<Pawn> here)
        {
            // together: read at the gathering itself, by the writer standing in its area with the others (Readings.Step's own look).
            if (r.next >= r.lines.Count) return false;
            if (r.speaker == null || !here.Contains(r.speaker))
            {
                r.missed++;
                if (ReadingMath.GivenUp(r.missed)) r.next = r.lines.Count;
                return false;
            }
            r.missed = 0;
            Readings.Say(r.speaker, r.lines[r.next]);
            r.next++;
            return true;
        }

        /// <summary>The gathering happened: those there remember the year read together, and the writer writes a line of it.</summary>
        public static void Over(GatheringReading r, PlayOut p, List<Pawn> took)
        {
            // together: they were at the reading itself (the play-out's own measure of taking part).
            if (took == null || r.next == 0) return;
            var def = TalkDefs.Mem("Rimfolk_Mem_YearRead");
            if (def != null) SharedTimes.AmongThem(took, def, r.label ?? p.label, 1f, 3);
            if (r.speaker != null && took.Contains(r.speaker))
                CouncilGameComponent.Instance?.journals.Write(r.speaker, "Rimfolk_Journal_YearRead", null, took.Count.Named("COUNT"));
        }

        /// <summary>The record keeper writes the year from now on (Annals.Author).</summary>
        public static Pawn Keeper(CouncilGameComponent comp, IList<Pawn> colonists) => comp?.life?.Keeper(colonists);
    }

    /// <summary>
    /// The council's answer makes someone the colony's record keeper ("Make A keeper of the colony's record",
    /// Council_DlmLv_RecordKeeper): they write the year letter from then on (A29).
    /// </summary>
    public class DilemmaEffect_RecordKeeper : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var p = PlayOuts.Who(PlayWho.A, ctx);
            if (p == null || !(ColonyCouncilMod.Settings?.annals ?? false)) return;
            sheet.Add(ImpactKind.Other, 1, "CC_Life_RecordKeeper_Impact".Translate(p.Named("PAWN")).Resolve(), "CC_Life_RecordKeeper_Brief".Translate(p.Named("PAWN")).Resolve(),
                "recordkeeper");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var book = LifeBook.Instance;
            var p = ctx?.a;
            if (book == null || p == null || p.Dead) return;
            book.recordKeeper = p;
        }
    }

    /// <summary>
    /// The year written up at Vanilla Books Expanded's writer's table (A29): a visit to the table in the day, and when it is
    /// done, a book called after the year (the game's novel; with the mod's printing researched, its newspaper), of the
    /// writer's own making, put on a shelf of the colony's (a bookcase with room), else by the table.
    /// </summary>
    public static class YearBook
    {
        public const string AfterPrefix = "yearbook|";

        private static readonly string[] Tables = { "VBE_WritersTable", "VBE_TypewritersTable" };

        /// <summary>The colony's writer's table they can reach, when the mod is there; null otherwise.</summary>
        public static Thing Table(Map map, Pawn writer)
        {
            if (map == null || writer == null) return null;
            var defs = Tables.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();
            if (defs.Count == 0) return null;
            return defs.SelectMany(d => map.listerThings.ThingsOfDef(d)).Where(t => t.Spawned && t.Faction == Faction.OfPlayer && writer.CanReach(t, Verse.AI.PathEndMode.InteractionCell, Danger.Some))
                .OrderBy(t => t.Position.DistanceToSquared(writer.Position)).FirstOrDefault();
        }

        public static void Plan(Pawn writer, Map map, string ord)
        {
            var table = Table(map, writer);
            if (table == null) return;
            var plan = new DilemmaEffect_Visit
            {
                who = PlayWho.A, to = PlaySpot.Place, label = "CC_Life_YearBook_Label".Translate(ord.Named("ORD")).Resolve(),
                hours = new IntRange(8, 15), duration = new FloatRange(1.5f, 2f), waitHours = 12f,
                work = DefDatabase<EffecterDef>.GetNamedSilentFail("Research")
            }.Begin(new HappeningContext { map = map, a = writer, place = table });
            if (plan != null) plan.afterward = AfterPrefix + ord;
        }

        /// <summary>The writing is done (PlayAfter): the book, named for the year, on the shelf.</summary>
        public static void Made(PlayOut p, string what)
        {
            var writer = p?.visitor;
            var map = p?.map;
            if (writer == null || map == null || what == null) return;
            string ord = what.Substring(AfterPrefix.Length);
            bool printed = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("VBE_Printing")?.IsFinished ?? false;
            var def = (printed ? DefDatabase<ThingDef>.GetNamedSilentFail("VBE_Newspaper") : null) ?? DefDatabase<ThingDef>.GetNamedSilentFail("Novel");
            if (def == null) return;
            var thing = ThingMaker.MakeThing(def);
            var quality = thing.TryGetComp<CompQuality>();
            quality?.SetQuality(QualityUtility.GenerateQualityCreatedByPawn(writer, SkillDefOf.Intellectual), ArtGenerationContext.Colony);
            string title = "CC_Life_YearBook_Title".Translate(ord.Named("ORD")).Resolve();
            if (thing is Book b) Name(b, title, "CC_Life_YearBook_Desc".Translate(writer.Named("PAWN"), ord.Named("ORD")).Resolve());
            bool shelved = Shelve(thing, map);
            if (!shelved && !GenPlace.TryPlaceThing(thing, (p.place ?? (Thing)writer).PositionHeld, map, ThingPlaceMode.Near))
            {
                if (!thing.Destroyed) thing.Destroy();
                return;
            }
            Tell.Message(NewsKind.Colony, Importance.Notable, map, (shelved ? "CC_Life_YearBook_Shelved" : "CC_Life_YearBook_Made")
                    .Translate(writer.Named("PAWN"), title.Named("TITLE"), def.label.Named("THING")).Resolve(),
                shelved ? new LookTargets(writer) : new LookTargets(thing), MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>The book's title and description: the game keeps them in fields of its own, set here when they can be found.</summary>
        private static void Name(Book b, string title, string desc)
        {
            try
            {
                AccessTools.Field(typeof(Book), "title")?.SetValue(b, title);
                AccessTools.Field(typeof(Book), "descriptionFlavor")?.SetValue(b, desc);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the year's book: its title", e);
            }
        }

        /// <summary>Onto a bookcase of the colony's with room for it; false when there is none.</summary>
        private static bool Shelve(Thing book, Map map)
        {
            foreach (var shelf in map.listerBuildings.allBuildingsColonist.OfType<Building_Bookcase>())
            {
                var held = shelf.GetDirectlyHeldThings();
                if (held == null || held.Count >= shelf.MaximumBooks || !shelf.Accepts(book)) continue;
                if (held.TryAdd(book)) return true;
            }
            return false;
        }
    }
}
