using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One of a faith's saints (or its damned): an Ideology deity, read as the colony reads it (Gods/Saints/Saints.cs, Of).</summary>
    public sealed class FaithSaint
    {
        public Ideo ideo;
        public string name, type;
        public Gender gender;
        /// <summary>The god the faith ties them to (SaintsMath.GodFor), or null.</summary>
        public God god;
        public bool damned;
        /// <summary>The word set their title is in (SaintsMath.WordSet, DamnedSet).</summary>
        public string set;
    }

    /// <summary>A saint as one of their places shows them (a grave, a statue, a relic): their title, their god, why, and whose.</summary>
    public sealed class SaintView
    {
        public string title, deed;
        public God god;
        public bool damned;
        public ColonySaint colony;
        public FaithSaint faith;
        public Ideo ideo;
        /// <summary>What the place is: their grave, a statue raised to them, a relic of theirs.</summary>
        public SaintPlaceKind kind;
    }

    public enum SaintPlaceKind
    {
        Grave,
        Statue,
        Relic
    }

    /// <summary>
    /// The saints (docs/gods-next-proposals.md, section F, option b; the player, 14 November 2026: "perhaps instead of these
    /// being the gods that are currently registered to an ideology, this could, for example, be some sort of saints or
    /// important characters. So kind of a middle ground between God and the pawns and the ideology, kind of saints or holy men
    /// or unholy men or whatever", then "let's go with saints then"). DESIGN.md "The saints" has the whole of it.
    /// <list type="bullet">
    /// <item>A faith's deities (Ideology's IdeoFoundation_Deity) are its saints, or its damned (<see cref="Of"/>), read only:
    /// the ideoligion is never changed, so the editor, a reform or a new roll of the deities simply gives new saints. Each is
    /// tied to one of the gods by its own words (GodsMath.DeityDomains, SaintsMath.GodFor), as the faith believes it, and
    /// shown "of" that god only where the colony knows the god (<see cref="OfGod"/>). A faith with no deities has none.</item>
    /// <item>The colony's own saints: after a remarkable death (SaintsMath.Merit) the council may name the fallen a saint
    /// (Gods/Saints/SaintsCouncil.cs). A person honoured, never their kin: nothing of it passes to anyone.</item>
    /// <item>Their places (Gods/Saints/SaintsPlaces.cs): their grave, a statue raised to them, a relic; the devoted pray there,
    /// and keep their day each year.</item>
    /// </list>
    /// The gods stay unseen: a saint is a person, named plainly; the god behind them is named only by the colony's name for it
    /// and only once the colony knows it.
    /// </summary>
    public static class Saints
    {
        /// <summary>The saints are the gods' and their worship's (the settings "Gods" and "Worship of the gods").</summary>
        public static bool On => Worship.On;

        public static SaintsBook Book => On ? SaintsBook.Instance : null;

        internal static int Now => Find.TickManager?.TicksGame ?? 0;

        private static readonly List<FaithSaint> NoSaints = new List<FaithSaint>();

        // ================= A faith's saints =================

        private static readonly Dictionary<int, (string sig, List<FaithSaint> list)> faithCache = new Dictionary<int, (string, List<FaithSaint>)>();

        /// <summary>
        /// A faith's saints and damned, in its deities' order: each deity's domains from its own words, its meme, the faith's
        /// memes and precepts (GodsMath.DeityDomains), whether it is one of the damned (SaintsMath.Damned) and the god the faith
        /// ties it to (SaintsMath.GodFor). Worked out again whenever the deities, the faith's age or the pantheon change.
        /// </summary>
        public static List<FaithSaint> Of(Ideo ideo)
        {
            if (!On || ideo == null || !ModsConfig.IdeologyActive) return NoSaints;
            try
            {
                if (!(ideo.foundation is IdeoFoundation_Deity f)) return NoSaints;
                var deities = f.DeitiesListForReading;
                if (deities == null || deities.Count == 0) return NoSaints;
                int age = AgeOf(ideo);
                string culture = ideo.culture?.defName;
                string sig = Gods.Version + "|" + age + "|" + culture + "|" + string.Join(";", deities.Select(d => d == null ? "" : d.name + "/" + d.type + "/" + d.relatedMeme?.defName))
                             + "|" + string.Join(",", ideo.memes?.Where(m => m != null).Select(m => m.defName) ?? Enumerable.Empty<string>());
                if (faithCache.TryGetValue(ideo.id, out var c) && c.sig == sig) return c.list;
                var memes = (ideo.memes ?? new List<MemeDef>()).Where(m => m != null).Select(m => (m.defName, (string)m.label)).ToList();
                var precepts = ideo.PreceptsListForReading?.Where(x => x?.def != null).Select(x => x.def.defName).ToList();
                var gods = Gods.All.Where(g => g != null && g.kind != GodKind.Dead).Select(g => (g.Key, g.domain, g.second, g.kind == GodKind.Hunger)).ToList();
                var taken = new List<GodDomain>();
                var list = new List<FaithSaint>();
                foreach (var d in deities)
                {
                    if (d == null || d.name.NullOrEmpty()) continue;
                    int seed = SaintsMath.Seed(d.name);
                    GodsMath.DeityDomains(d.name, d.type, d.relatedMeme?.defName, d.relatedMeme?.label, memes, precepts, taken, age, seed, out var domain, out var second);
                    taken.Add(domain);
                    bool damned = SaintsMath.Damned(d.relatedMeme?.defName, d.relatedMeme?.label, d.name, d.type, domain);
                    string key = SaintsMath.GodFor(domain, second, damned, gods, seed);
                    list.Add(new FaithSaint
                    {
                        ideo = ideo, name = d.name, type = d.type, gender = d.gender, god = Gods.ByKey(key), damned = damned,
                        set = damned ? SaintsMath.DamnedSet(age) : SaintsMath.WordSet(age, culture)
                    });
                }
                faithCache[ideo.id] = (sig, list);
                return list;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saints: a faith's saints", e);
                return NoSaints;
            }
        }

        /// <summary>
        /// A faith's age, for its words: the colony's where its own people hold it, else the first people whose faith it is
        /// (their tech level), else the medieval.
        /// </summary>
        public static int AgeOf(Ideo ideo)
        {
            if (ideo == null) return (int)Ages.Ours;
            try
            {
                var player = Faction.OfPlayerSilentFail;
                if (player?.ideos != null && (player.ideos.PrimaryIdeo == ideo || player.ideos.Has(ideo))) return (int)Ages.Ours;
                foreach (var f in Find.FactionManager?.AllFactionsListForReading ?? new List<Faction>())
                    if (f != null && !f.IsPlayer && f.ideos?.PrimaryIdeo == ideo)
                        return (int)Ages.Of(f);
            }
            catch
            {
                // The faith's people unread: the medieval word.
            }
            return 1;
        }

        /// <summary>The colony's faiths: the primary faith of each of its own people at its homes, the most held first.</summary>
        public static List<Ideo> ColonyFaiths()
        {
            if (!ModsConfig.IdeologyActive) return new List<Ideo>();
            var count = new Dictionary<Ideo, int>();
            foreach (var map in Homes.All)
                foreach (var p in Homes.People(map))
                    if (p?.Ideo != null && Worship.Ours(p))
                        count[p.Ideo] = (count.TryGetValue(p.Ideo, out int n) ? n : 0) + 1;
            return count.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        }

        // ================= Words =================

        /// <summary>A saint's title in their word set: "Saint Aranea", "Ancestor Aranea", "Vorgath the Damned".</summary>
        public static string Title(string name, string set) => ("CC_Saint_Title_" + (set ?? "Saints")).Translate((name ?? "?").Named("NAME")).Resolve();

        public static string Title(FaithSaint s) => s == null ? "" : Title(s.name, s.set);

        /// <summary>A word set's plural, as a header: "Saints", "Ancestors", "the Damned".</summary>
        public static string Plural(string set) => ("CC_Saint_Word_" + (set ?? "Saints")).Translate().Resolve();

        /// <summary>The word a faith's deities are shown under on the ideoligion screen: its saints', or its damned's when every one is damned.</summary>
        public static string HeaderWord(Ideo ideo)
        {
            var list = Of(ideo);
            int age = AgeOf(ideo);
            int damned = list.Count(s => s.damned);
            return Plural(SaintsMath.HeaderSet(age, ideo?.culture?.defName, list.Count - damned, damned));
        }

        /// <summary>A god named after a saint, as "of" it: the colony's name for it, only where the colony knows it; else null.</summary>
        public static string OfGod(God g) => g != null && Gods.Known(g) && g.kind != GodKind.Dead ? Worship.Called(g) : null;

        /// <summary>The word set of the colony's own saints, by the colony's age when one was named and their faith's culture.</summary>
        public static string SetOf(ColonySaint s) => SaintsMath.WordSet(s?.age ?? 1, s?.ideo?.culture?.defName);

        public static string Title(ColonySaint s) => s == null ? "" : Title(s.name, SetOf(s));

        /// <summary>Why a colony saint is one, in a phrase: "who kept the sick for years".</summary>
        public static string Deed(ColonySaint s) => s == null ? "" : ("CC_Saint_Deed_" + s.reason).Translate().Resolve();

        /// <summary>"a saint of Hollin", "an Ancestor who walks with Hollin"; "a saint" where the colony doesn't know the god.</summary>
        public static string ASaint(string set, God g)
        {
            string of = OfGod(g);
            return of == null ? ("CC_Saint_A_" + set).Translate().Resolve() : ("CC_Saint_AOf_" + set).Translate(of.Named("GOD")).Resolve();
        }

        /// <summary>
        /// The faith's holy one a neighbour's sermon or hymn names ({DEITY}, Letters/LetterFormShapes.cs): now and then one of the
        /// colony's own saints where the writer shares their faith, else its first saint, by title; null with none (the game's
        /// own name for its god stands).
        /// </summary>
        public static string SermonName(Ideo ideo)
        {
            if (!On || ideo == null) return null;
            try
            {
                var ours = Book?.saints.Where(s => s != null && s.ideo == ideo).ToList();
                if (ours != null && ours.Count > 0 && Rand.Chance(0.35f)) return Title(ours.RandomElement());
                var holy = Of(ideo).FirstOrDefault(s => !s.damned);
                return holy != null ? Title(holy) : null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saints: a sermon's saint", e);
                return null;
            }
        }

        // ================= What a saint gives their god =================

        /// <summary>
        /// A colonist's faith's saints lean them toward the saints' gods a little more (Gods/Belief/PawnGods.cs, Sketch: the faith's
        /// lean, SaintsMath.WithSaints); never toward the damned's.
        /// </summary>
        public static void Lean(Pawn p, BeliefSketch s)
        {
            if (!On || p?.Ideo == null || s == null) return;
            try
            {
                var gods = Of(p.Ideo).Where(x => !x.damned && x.god != null).Select(x => x.god.Key).ToList();
                if (gods.Count == 0) return;
                s.faith = SaintsMath.WithSaints(s.faith, gods);
                if (s.faithName.NullOrEmpty()) s.faithName = p.Ideo.name;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saints: a faith's lean", e);
            }
        }

        // ================= The colony's own saints =================

        /// <summary>A colony saint by the dead pawn, or null.</summary>
        public static ColonySaint OfDead(Pawn dead) => dead == null ? null : Book?.ByPawn(dead.ThingID);

        /// <summary>
        /// A colonist died: if the death merits a saint (SaintsMath.Merit), it waits for the council's card, with the god they
        /// held first (else the one their deed speaks of: Worship.GodOf); never one who held the Hunger.
        /// </summary>
        public static void Died(Pawn p, SaintDeath d)
        {
            var b = Book;
            if (b == null || p == null) return;
            var reason = SaintsMath.Merit(d, out _);
            if (reason == SaintReason.None || b.ByPawn(p.ThingID) != null || b.waiting.Any(c => c.pawnId == p.ThingID)) return;
            var g = PawnGods.First(p) ?? Worship.GodOf(p, SaintsMath.ReasonDomain(reason));
            if (g == null || g.kind == GodKind.Hunger || g.kind == GodKind.Dead) return;
            var map = p.MapHeld != null && Homes.IsHome(p.MapHeld) ? p.MapHeld : Homes.Seat;
            if (map == null) return;
            b.waiting.Add(new SaintCandidate
            {
                pawnId = p.ThingID, name = p.LabelShort, fullName = p.Name?.ToStringFull ?? p.LabelShort, gender = p.gender, god = g.id,
                ideo = ModsConfig.IdeologyActive ? p.Ideo : null, reason = reason, died = Now, feastDay = GenLocalDate.DayOfYear(map), home = Gods.HomeKey(map)
            });
            b.Trim();
        }

        /// <summary>The death waiting longest at a home that still may be asked about (taken off the list), or null.</summary>
        public static SaintCandidate Waiting(Map map, bool take)
        {
            var b = Book;
            if (b == null || map == null) return null;
            int home = Gods.HomeKey(map);
            var c = b.waiting.Where(x => x != null && x.home == home && Now - x.died <= SaintsMath.CandidateDays * GenDate.TicksPerDay).OrderBy(x => x.died).FirstOrDefault();
            if (c != null && take) b.waiting.Remove(c);
            return c;
        }

        /// <summary>
        /// The council names the fallen a saint: kept in the colony's own list, written in its book under their god by those at
        /// the words, a small good mood for the colony's people (never more for kin: a person honoured, never their family), a
        /// relic of theirs set aside by the grave (or by the one who asked), and a line in that one's journal.
        /// </summary>
        public static ColonySaint Name(SaintCandidate c, Map map, Pawn by)
        {
            var b = Book;
            if (b == null || c == null || map == null) return null;
            var have = b.ByPawn(c.pawnId);
            if (have != null) return have;
            var s = new ColonySaint
            {
                id = b.nextId++, name = c.name, fullName = c.fullName, pawnId = c.pawnId, gender = c.gender, god = c.god, ideo = c.ideo,
                faithName = c.ideo?.name, reason = c.reason, died = c.died, named = Now, feastDay = c.feastDay, namedYear = GenLocalDate.Year(map),
                home = c.home, age = (int)Ages.Ours
            };
            b.saints.Add(s);
            b.Trim();
            try
            {
                var g = Gods.ById(s.god);
                var grave = PlaySpots.GraveOf(map, s.pawnId);
                var cell = grave?.Position ?? by?.Position ?? IntVec3.Invalid;
                string words = "CC_Saint_Book_Named".Translate(s.name.Named("DEAD"), ASaint(SetOf(s), g).Named("ASAINT"), Deed(s).Named("DEED")).Resolve();
                if (g != null) Worship.Booked(g, map, cell, words, by);
                if (g != null && cell.IsValid) Worship.Glow(map, cell, g);
                // apart: the colony's own people hear of it as the council's word, wherever they are.
                foreach (var p in Homes.People(map).Where(x => Worship.Ours(x) && !Worship.Scoffer(x)))
                    CouncilGameComponent.GiveThought(p, SaintsDefOf.Rimfolk_Saint_Named, null, -1f, words);
                Relic(s, map, grave, by);
                if (by != null)
                {
                    string line = Worship.LineWith("Rimfolk_Pool_Saint_NamedJournal", by, g, null, Args(s), OfGod(g) != null ? "known" : null);
                    if (!line.NullOrEmpty()) CouncilGameComponent.Instance?.journals.WriteText(by, line);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saints: a saint named", e);
            }
            return s;
        }

        /// <summary>A saint's own placeholders for a line: {SAINT} their title, {DEAD} their name, {DEED} why.</summary>
        public static NamedArgument[] Args(ColonySaint s) => new[] { Title(s).Named("SAINT"), (s?.name ?? "").Named("DEAD"), Deed(s).Named("DEED") };

        /// <summary>A view's placeholders for a line: {SAINT}, {DEAD}, {DEED}.</summary>
        public static NamedArgument[] Args(SaintView v) => new[] { (v?.title ?? "").Named("SAINT"), (v?.colony?.name ?? v?.faith?.name ?? "").Named("DEAD"), (v?.deed ?? "").Named("DEED") };

        /// <summary>
        /// A relic of the saint set aside: a keepsake of theirs in the colony's age's form (a lock of hair bound in cord, a
        /// finger bone in a small box, a lock of hair under glass, their voice kept on a chip), laid by their grave, else by the
        /// one who asked; told plainly, so what the words say is what the map shows.
        /// </summary>
        private static void Relic(ColonySaint s, Map map, Thing grave, Pawn by)
        {
            var def = SaintsDefOf.Rimfolk_SaintRelic;
            if (def == null || map == null) return;
            var at = grave != null && grave.Spawned && grave.Map == map ? grave.Position : by != null && by.Spawned && by.Map == map ? by.Position : IntVec3.Invalid;
            if (!at.IsValid) return;
            var t = ThingMaker.MakeThing(def);
            var comp = t.TryGetComp<CompSaintRelic>();
            if (comp != null)
            {
                comp.saint = s.id;
                comp.age = s.age;
            }
            if (!GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near)) return;
            string form = ("CC_Saint_RelicForm_" + Mathf.Clamp(s.age, 0, 3)).Translate().Resolve();
            Tell.Message(NewsKind.Colony, Importance.Notable, map,
                (grave != null ? "CC_Saint_RelicSet_Grave" : "CC_Saint_RelicSet").Translate(Title(s).Named("SAINT"), form.Named("FORM"), (by?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                t, MessageTypeDefOf.PositiveEvent);
        }

        // ================= The passes =================

        /// <summary>The region's hour (Worship.Hourly): a saint's day kept, prayers at the saints' places, statues raised, a likeness known.</summary>
        public static void Hourly(Map map, int now)
        {
            if (Book == null || map == null) return;
            Worship.Safe("saints: a saint's day", () => SaintsPlaces.Feasts(map, now));
            Worship.Safe("saints: prayers at their places", () => SaintsPlaces.Prayers(map, now));
            Worship.Safe("saints: statues raised", () => SaintsPlaces.Raisings(map, now));
            if (GenLocalDate.HourOfDay(map) == 7) Worship.Safe("saints: a likeness", () => SaintsPlaces.Likenesses(map));
        }

        /// <summary>The region's day (Worship.Daily): the deaths waited too long let go, the faith's saints seen, its damned taken to heart.</summary>
        public static void Daily(int now)
        {
            var b = Book;
            if (b == null) return;
            b.waiting.RemoveAll(c => c == null || now - c.died > SaintsMath.CandidateDays * GenDate.TicksPerDay);
            Worship.Safe("saints: the faith's saints seen", () => Seen(b, now));
            Worship.Safe("saints: the damned", () => DamnedFeared(b));
            b.Trim();
        }

        /// <summary>The colony's faiths' saints, kept as seen; one no longer among them marked kept no more.</summary>
        private static void Seen(SaintsBook b, int now)
        {
            foreach (var ideo in ColonyFaiths())
            {
                var list = Of(ideo);
                foreach (var s in list)
                {
                    var had = b.seen.FirstOrDefault(x => x.ideo == ideo && x.name == s.name);
                    if (had == null)
                        b.seen.Add(new FaithSaintSeen { ideo = ideo, faithName = ideo.name, name = s.name, set = s.set, god = s.god?.id ?? -1, damned = s.damned, first = now });
                    else
                    {
                        had.gone = -1;
                        had.faithName = ideo.name;
                    }
                }
                foreach (var x in b.seen.Where(x => x.ideo == ideo && x.gone < 0 && list.All(s => s.name != x.name)))
                    x.gone = now;
            }
        }

        /// <summary>
        /// Each of the colony's people takes their faith's damned to heart once: uneasy about the god each is tied to
        /// (SaintsMath.DamnedDread), a pawn fact with its line of why, never a prayer to it.
        /// </summary>
        private static void DamnedFeared(SaintsBook b)
        {
            foreach (var map in Homes.All)
                foreach (var p in Homes.People(map))
                {
                    if (!Worship.Ours(p) || p.Ideo == null || Worship.Scoffer(p)) continue;
                    foreach (var s in Of(p.Ideo))
                    {
                        if (!s.damned || s.god == null || !Gods.Known(s.god)) continue;
                        string key = p.thingIDNumber + "|" + s.god.id + "|" + s.name;
                        if (b.feared.Contains(key)) continue;
                        b.feared.Add(key);
                        PawnGods.Fear(p, s.god, SaintsMath.DamnedDread,
                            "CC_Saint_Why_Damned".Translate(Title(s).Named("SAINT"), (p.Ideo.name ?? "").Named("FAITH")).Resolve());
                    }
                }
        }

        // ================= The book =================

        /// <summary>
        /// The saints under a god's chapter in the colony's book (GodLorePage), in its keeper's hand: the colony's own named for
        /// it, the colony's faiths' saints the faith ties to it, its damned, and those kept no more; nothing while there are none.
        /// Only under the chapter the colony keeps that god under, so a god is never named where the colony doesn't know it.
        /// </summary>
        public static List<string> BookLines(God g)
        {
            var b = Book;
            if (b == null || g == null) return new List<string>();
            // Worked out at most once a second (the book draws every frame).
            float at = Time.realtimeSinceStartup;
            if (bookCache.TryGetValue(g.id, out var c) && at - c.at < 1f && at >= c.at) return c.lines;
            var lines = new List<string>();
            bookCache[g.id] = (at, lines);
            foreach (var s in b.saints.Where(x => x != null && x.god == g.id))
                lines.Add("CC_Saint_Book_Ours".Translate(Title(s).Named("SAINT"), Deed(s).Named("DEED"), GodLorePage.Date(s.named).Named("DATE"),
                    ("CC_Saint_Book_Day_" + s.gender).Translate().Named("DAY")).Resolve());
            foreach (var ideo in ColonyFaiths())
                foreach (var s in Of(ideo).Where(x => x.god == g))
                    lines.Add((s.damned ? "CC_Saint_Book_Damned" : "CC_Saint_Book_Faith").Translate((ideo.name ?? "").Named("FAITH"), Title(s).Named("SAINT"),
                        Plural(s.set).ToLowerInvariant().Named("WORD")).Resolve());
            foreach (var x in b.seen.Where(x => x != null && x.gone >= 0 && x.god == g.id))
                lines.Add("CC_Saint_Book_Gone".Translate(Title(x.name, x.set).Named("SAINT"), (x.faithName ?? "").Named("FAITH")).Resolve());
            return lines;
        }

        private static readonly Dictionary<int, (float at, List<string> lines)> bookCache = new Dictionary<int, (float, List<string>)>();

        // ================= Places =================

        /// <summary>A saint as a place shows them: a relic of theirs, a statue raised to them (its words said), or their grave; null for anything else.</summary>
        public static SaintView ViewAt(Thing t)
        {
            var b = Book;
            if (b == null || t == null) return null;
            var relic = t.TryGetComp<CompSaintRelic>();
            if (relic != null)
            {
                var s = b.ById(relic.saint);
                return s == null ? null : View(s, SaintPlaceKind.Relic);
            }
            var place = b.PlaceOf(t);
            if (place != null && !place.pending) return View(place);
            if (t is Building_Grave grave && grave.HasCorpse)
            {
                var s = b.ByPawn(grave.Corpse?.InnerPawn?.ThingID);
                if (s != null) return View(s, SaintPlaceKind.Grave);
            }
            return null;
        }

        public static SaintView View(ColonySaint s, SaintPlaceKind kind) => s == null
            ? null
            : new SaintView { title = Title(s), deed = Deed(s), god = Gods.ById(s.god), colony = s, ideo = s.ideo, kind = kind };

        /// <summary>A statue's saint: the colony's own, or a faith's by name (its god as the faith ties it).</summary>
        public static SaintView View(SaintPlace p)
        {
            if (p == null) return null;
            if (p.saint >= 0)
            {
                var s = Book?.ById(p.saint);
                return s == null ? null : View(s, SaintPlaceKind.Statue);
            }
            var faith = p.ideo == null ? null : Of(p.ideo).FirstOrDefault(x => x.name == p.name);
            return new SaintView
            {
                title = Title(p.name, faith?.set ?? p.set), deed = "", god = faith?.god ?? Gods.ById(p.god), faith = faith, ideo = p.ideo,
                damned = faith?.damned ?? false, kind = SaintPlaceKind.Statue
            };
        }

        /// <summary>The faith's holy relic precept (Ideology): its believers venerate a saint's relic as a relic.</summary>
        public static bool RelicFaith(Pawn p)
        {
            if (!ModsConfig.IdeologyActive || p?.Ideo == null) return false;
            try
            {
                return p.Ideo.PreceptsListForReading?.Any(x => x is Precept_Relic) ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    [DefOf]
    public static class SaintsDefOf
    {
        public static ThoughtDef Rimfolk_Saint_Named;
        public static ThoughtDef Rimfolk_Saint_Prayed;
        public static ThoughtDef Rimfolk_Saint_PrayedRelic;
        public static ThoughtDef Rimfolk_Saint_FeastKept;
        public static JobDef Rimfolk_Saint_Pray;
        public static JobDef Rimfolk_Saint_Raise;
        public static ThingDef Rimfolk_SaintRelic;

        static SaintsDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SaintsDefOf));
        }
    }
}
