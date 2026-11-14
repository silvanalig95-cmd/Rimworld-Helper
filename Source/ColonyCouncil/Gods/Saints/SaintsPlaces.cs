using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The saints' places (docs/gods-next-proposals.md, section F; DESIGN.md "The saints"): a colony saint's grave, a statue
    /// raised to a saint (the game's own sculptures, Odyssey's statue, Vanilla Furniture Expanded - Art's and Alpha Memes'
    /// statues, by SaintsMath.IsStatue), and a relic of theirs. Each is a lesser shrine: the devoted walk there now and then in
    /// their free time and pray to the saint (a sentence, a small good mood, their belief in the saint's god a little
    /// stronger), and a colony saint's day is kept there each year by the devoted. A statue is raised to a saint by the player's
    /// word (its button): someone walks over and says the words, and the art takes the saint's name as its title. A statue
    /// that is already the fallen one's likeness (Odyssey's statue of a person; the seam <see cref="LikenessComps"/> takes
    /// Pawns on Display's and the like) is known for theirs. The damned have no places: nobody prays to them.
    /// </summary>
    public static class SaintsPlaces
    {
        // ================= Who is devoted to a saint =================

        /// <summary>
        /// Someone who would pray to a saint and keep their day: they hold the saint's god (from leaning toward it up, not in
        /// dread of it), or they are devout in the saint's own faith, or they are the priest.
        /// </summary>
        public static bool Devotee(Pawn p, God g, Ideo ideo)
        {
            if (p == null || !Worship.Ours(p) || Worship.Scoffer(p)) return false;
            if (g != null)
            {
                var w = PawnGods.WordOf(p, g);
                if (w == BeliefWord.Leans || w == BeliefWord.Holds || w == BeliefWord.Devoted || w == BeliefWord.Fervent) return true;
            }
            if (ideo != null && p.Ideo == ideo && Worship.Devout(p)) return true;
            return Worship.IsPriest(p);
        }

        /// <summary>The saints' places on a map, each with the saint it shows: graves, statues (their words said) and relics; never one of the damned's.</summary>
        public static List<(Thing thing, SaintView view)> On(Map map)
        {
            var list = new List<(Thing, SaintView)>();
            var b = Saints.Book;
            if (b == null || map == null) return list;
            foreach (var s in b.saints)
            {
                var grave = s == null ? null : PlaySpots.GraveOf(map, s.pawnId);
                if (grave != null) list.Add((grave, Saints.View(s, SaintPlaceKind.Grave)));
            }
            foreach (var p in b.places)
                if (p != null && !p.pending && p.Live && p.thing.Map == map)
                {
                    var v = Saints.View(p);
                    if (v != null && !v.damned) list.Add((p.thing, v));
                }
            var relic = SaintsDefOf.Rimfolk_SaintRelic;
            if (relic != null)
                foreach (var t in map.listerThings.ThingsOfDef(relic))
                {
                    var v = Saints.ViewAt(t);
                    if (v != null && t.Spawned && !t.Position.Fogged(map)) list.Add((t, v));
                }
            return list;
        }

        // ================= A saint's day =================

        /// <summary>The region's hour: a colony saint's day come round (SaintsMath.FeastDue) is kept by the devoted at their grave or statue, one a home an hour.</summary>
        public static void Feasts(Map map, int now)
        {
            var b = Saints.Book;
            int home = Gods.HomeKey(map);
            int day = GenLocalDate.DayOfYear(map), hour = GenLocalDate.HourOfDay(map), year = GenLocalDate.Year(map);
            foreach (var s in b.saints)
            {
                if (s == null || s.home != home || !SaintsMath.FeastDue(day, hour, year, s.feastDay, s.namedYear, s.lastFeastYear)) continue;
                s.lastFeastYear = year;
                Keep(s, map, now);
                return;
            }
        }

        /// <summary>
        /// A saint's day kept: those devoted to them gather at their grave (else a statue raised to them, else their god's
        /// place, else where the colony gathers) for an hour's words, led by the priest or the most devoted, the words their
        /// sermon on the saint (the play-out, PlayOut/: a small good mood for those who came); their belief in the saint's god
        /// grows as at a festival. With answers not played out on the map, the devoted keep it quietly where they are.
        /// </summary>
        public static bool Keep(ColonySaint s, Map map, int now)
        {
            var g = Gods.ById(s.god);
            var who = Homes.People(map).Where(p => p.Spawned && p.Map == map && !p.DevelopmentalStage.Baby() && Devotee(p, g, s.ideo)).ToList();
            if (who.Count == 0) return false;
            Thing place = PlaySpots.GraveOf(map, s.pawnId)
                          ?? Saints.Book.places.FirstOrDefault(p => p != null && !p.pending && p.saint == s.id && p.Live && p.thing.Map == map)?.thing
                          ?? (g == null ? null : Shrines.Of(map, g).Select(x => x.thing).FirstOrDefault());
            var priest = Worship.Priest(map);
            var lead = priest != null && who.Contains(priest) ? priest : who.OrderByDescending(p => g == null ? 0f : PawnGods.HoldOf(p, g)).First();
            string title = Saints.Title(s);
            string label = "CC_Saint_FeastLabel".Translate(title.Named("SAINT")).Resolve();
            string cause = "CC_Saint_Cause_Feast".Translate(title.Named("SAINT")).Resolve();
            PawnGods.Festival(who, g, label);
            var lines = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                string line = Worship.LineWith("Rimfolk_Pool_Saint_Feast", lead, g, null, Saints.Args(s), Saints.OfGod(g) != null ? "known" : null);
                if (!line.NullOrEmpty() && !lines.Contains(line)) lines.Add(line);
            }
            var tracker = PlayOutTracker.Instance;
            if (PlayOuts.On && tracker != null)
            {
                try
                {
                    var p = new PlayOut
                    {
                        kind = PlayKind.Toast, map = map, created = now, label = label, words = true,
                        invited = who.Where(PlayOuts.OurPerson).ToList(), organizer = lead,
                        spots = place != null ? new List<PlaySpot> { PlaySpot.Place, PlaySpot.Gather, PlaySpot.Dining } : new List<PlaySpot> { PlaySpot.Gather, PlaySpot.Dining, PlaySpot.Outdoors },
                        place = place, durationTicks = PlayOutMath.DurationTicks(1f), lines = lines, hourFrom = SaintsMath.FeastFrom, hourTo = 23
                    };
                    if (p.invited.Count > 0)
                    {
                        p.deadline = PlayOutMath.Deadline(now, GenLocalDate.HourOfDay(map), p.hourFrom, p.hourTo, 4f);
                        foreach (var x in p.invited) p.moods.Add(new HeldMood { pawn = x, thought = SaintsDefOf.Rimfolk_Saint_FeastKept, cause = cause });
                        tracker.Add(p);
                        return true;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("saints: a saint's day played out", e);
                }
            }
            // apart: each keeps the day where they are, with no gathering.
            foreach (var p in who) CouncilGameComponent.GiveThought(p, SaintsDefOf.Rimfolk_Saint_FeastKept, null, -1f, cause);
            Tell.Minor(NewsKind.Colony, Importance.Flavor, map, "CC_Saint_FeastQuiet".Translate(title.Named("SAINT"), PlayWords.Names(who).Named("WHO")).Resolve(), place);
            return true;
        }

        // ================= Prayers at a saint's place =================

        /// <summary>
        /// The region's hour: now and then (SaintsMath.PrayChance, more with more places) one of the devoted, free and near
        /// enough, walks to a saint's place to pray (SaintsMath.PrayGapDays apart for each).
        /// </summary>
        public static void Prayers(Map map, int now)
        {
            var def = SaintsDefOf.Rimfolk_Saint_Pray;
            if (def == null) return;
            var places = On(map);
            if (places.Count == 0 || !Rand.Chance(SaintsMath.PrayChance(places.Count))) return;
            var (t, v) = places.RandomElement();
            foreach (var p in map.mapPawns.FreeColonistsSpawned.InRandomOrder().ToList())
            {
                if (p.DevelopmentalStage.Baby() || !Devotee(p, v.god, v.ideo) || !WorshipJobs.Free(p)) continue;
                if (!p.CanReach(t, PathEndMode.Touch, Danger.None)) continue;
                if (!Interplay.Ready("saint-prayer|" + p.thingIDNumber, SaintsMath.PrayGapDays)) continue;
                WorshipJobs.Start(p, JobMaker.MakeJob(def, t));
                return;
            }
        }

        /// <summary>Dev: someone prays at the nearest saint's place now.</summary>
        public static bool DevPray(Pawn p)
        {
            var def = SaintsDefOf.Rimfolk_Saint_Pray;
            if (def == null || p?.Map == null) return false;
            var t = On(p.Map).Select(x => x.thing).Where(x => p.CanReach(x, PathEndMode.Touch, Danger.Deadly)).OrderBy(x => x.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            return t != null && WorshipJobs.Start(p, JobMaker.MakeJob(def, t));
        }

        /// <summary>Arrived at a saint's place: a sentence to the saint (by the colony's name for their god where it knows it).</summary>
        public static void PrayArrive(Pawn p, Thing t)
        {
            var v = Saints.ViewAt(t);
            if (v == null) return;
            string line = Worship.LineWith("Rimfolk_Pool_Saint_Prayer", p, v.god, null, Saints.Args(v), Saints.OfGod(v.god) != null ? "known" : null,
                "place:" + v.kind.ToString().ToLowerInvariant());
            if (!line.NullOrEmpty()) Speech.Say(p, line);
            if (p.Map != null) FleckMaker.ThrowMetaIcon(p.Position, p.Map, FleckDefOf.Meditating);
        }

        /// <summary>The prayer said: their belief in the saint's god a little stronger (as a prayer at its own place), and a small good mood, more before a relic where their faith keeps relics.</summary>
        public static void Prayed(Pawn p, Thing t)
        {
            var v = Saints.ViewAt(t);
            if (v == null) return;
            if (v.god != null) PawnGods.Prayed(p, v.god, true);
            bool relic = v.kind == SaintPlaceKind.Relic && Saints.RelicFaith(p);
            CouncilGameComponent.GiveThought(p, relic ? SaintsDefOf.Rimfolk_Saint_PrayedRelic : SaintsDefOf.Rimfolk_Saint_Prayed, null, -1f,
                "CC_Saint_Cause_Prayed".Translate(v.title.Named("SAINT")).Resolve());
        }

        // ================= A statue raised to a saint =================

        /// <summary>A building of the colony's that can be raised to a saint: a statue or a sculpture (SaintsMath.IsStatue).</summary>
        public static bool IsStatue(Thing t) => t is Building b && b.Spawned && b.Faction == Faction.OfPlayer && b.def != null && !b.def.IsFrame && !b.def.IsBlueprint
                                                && SaintsMath.IsStatue(b.def.defName, b.def.IsArt);

        /// <summary>The saints a statue can be raised to: the colony's own, then the holy saints of its people's faiths (never the damned).</summary>
        public static List<(string label, Action<Thing> raise)> Choices()
        {
            var list = new List<(string, Action<Thing>)>();
            var b = Saints.Book;
            if (b == null) return list;
            foreach (var s in b.saints.Where(x => x != null).Reverse())
            {
                var saint = s;
                list.Add(("CC_Saint_Menu_Ours".Translate(Saints.Title(saint).Named("SAINT")).Resolve(), t => Ask(t, saint.id, null, null, saint.god, null)));
            }
            foreach (var ideo in Saints.ColonyFaiths())
                foreach (var f in Saints.Of(ideo).Where(x => !x.damned))
                {
                    var fs = f;
                    list.Add(("CC_Saint_Menu_Faith".Translate(Saints.Title(fs).Named("SAINT"), (ideo.name ?? "").Named("FAITH")).Resolve(),
                        t => Ask(t, -1, fs.name, fs.ideo, fs.god?.id ?? -1, fs.set)));
                }
            return list;
        }

        /// <summary>The player asks for a statue to be raised to a saint: someone will walk over and say the words.</summary>
        public static void Ask(Thing t, int saint, string name, Ideo ideo, int god, string set)
        {
            var b = Saints.Book;
            if (b == null || t == null) return;
            var r = b.PlaceOf(t);
            if (r == null)
            {
                b.places.Add(r = new SaintPlace { thing = t });
                b.Trim();
            }
            r.saint = saint;
            r.name = name;
            r.ideo = ideo;
            r.god = god;
            r.set = set;
            r.pending = true;
            r.asked = Saints.Now;
            var v = Saints.View(r);
            Messages.Message("CC_Saint_RaiseAsked".Translate(t.LabelShort.Named("PLACE"), (v?.title ?? "?").Named("SAINT")).Resolve(), t, MessageTypeDefOf.SilentInput, false);
        }

        /// <summary>The player takes a statue from its saint: its art's own title given back.</summary>
        public static void Clear(Thing t)
        {
            var b = Saints.Book;
            var r = b?.PlaceOf(t);
            if (r == null) return;
            var art = t.TryGetComp<CompArt>();
            if (art != null && r.oldTitle != null && !r.pending)
                try
                {
                    art.Title = r.oldTitle;
                }
                catch
                {
                    // The art's title stays.
                }
            b.places.Remove(r);
        }

        /// <summary>The region's hour: a statue waiting to be raised gets someone to go and say the words; too long waiting, it is let go.</summary>
        public static void Raisings(Map map, int now)
        {
            var b = Saints.Book;
            var def = SaintsDefOf.Rimfolk_Saint_Raise;
            if (def == null) return;
            foreach (var r in b.places.Where(x => x != null && x.pending).ToList())
            {
                if (!r.Live || r.thing.Map != map) continue;
                if (now - r.asked > WorshipMath.DedicateWaitDays * GenDate.TicksPerDay)
                {
                    b.places.Remove(r);
                    Tell.Minor(NewsKind.Colony, Importance.Flavor, map, "CC_Saint_RaiseLetGo".Translate(r.thing.LabelShort.Named("PLACE")).Resolve(), r.thing);
                    continue;
                }
                if (map.mapPawns.FreeColonistsSpawned.Any(p => p.CurJobDef == def && p.CurJob.targetA.Thing == r.thing)) continue;
                var v = Saints.View(r);
                var who = Raiser(map, v, r.thing);
                if (who != null) WorshipJobs.Start(who, JobMaker.MakeJob(def, r.thing));
            }
        }

        /// <summary>Who raises it: the priest, else a free devotee of the saint, else anyone free (never a scoffer).</summary>
        private static Pawn Raiser(Map map, SaintView v, Thing t)
        {
            var priest = Worship.Priest(map);
            if (priest != null && priest.Map == map && WorshipJobs.Free(priest) && priest.CanReach(t, PathEndMode.Touch, Danger.Some)) return priest;
            return map.mapPawns.FreeColonistsSpawned.Where(p => Worship.Ours(p) && !p.DevelopmentalStage.Juvenile() && !Worship.Scoffer(p) && WorshipJobs.Free(p)
                                                                && p.CanReach(t, PathEndMode.Touch, Danger.Some))
                .OrderByDescending(p => Devotee(p, v?.god, v?.ideo) ? 1 : 0).ThenBy(p => p.Position.DistanceToSquared(t.Position)).FirstOrDefault();
        }

        public static void RaiseArrive(Pawn p, Thing t)
        {
            var v = Saints.View(Saints.Book?.PlaceOf(t));
            if (v == null) return;
            string line = Worship.LineWith("Rimfolk_Pool_Saint_Raise", p, v.god, null, Saints.Args(v), Saints.OfGod(v.god) != null ? "known" : null);
            if (!line.NullOrEmpty()) Speech.Say(p, line);
            Worship.PriestLight(p, t.Position);
        }

        /// <summary>The words said: the statue is the saint's, its art titled with their name, a glow, and the news says so.</summary>
        public static void Raised(Pawn p, Thing t)
        {
            var r = Saints.Book?.PlaceOf(t);
            if (r == null || !r.pending || !r.Live) return;
            var v = Saints.View(r);
            if (v == null) return;
            r.pending = false;
            r.since = Saints.Now;
            r.byName = p?.LabelShort;
            Title(t, r, v.title);
            var map = t.Map;
            if (v.god != null) Worship.Glow(map, t.Position, v.god);
            string text = "CC_Saint_Raised".Translate((p?.LabelShort ?? "?").Named("PAWN"), t.LabelShort.Named("PLACE"), v.title.Named("SAINT")).Resolve();
            Tell.Message(NewsKind.Colony, Importance.Notable, map, text, t, MessageTypeDefOf.PositiveEvent);
            if (v.god != null) Worship.Booked(v.god, map, t.Position, text, p);
        }

        /// <summary>The art takes the saint's title, its own kept to give back.</summary>
        private static void Title(Thing t, SaintPlace r, string title)
        {
            var art = t.TryGetComp<CompArt>();
            if (art == null) return;
            try
            {
                if (r.oldTitle == null) r.oldTitle = art.Title ?? "";
                art.Title = title;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saints: a statue's title", e);
            }
        }

        // ================= A likeness =================

        /// <summary>
        /// The comps that keep whose likeness a statue is, and the field that names them: Odyssey's statue of a person. Pawns on
        /// Display (not in the player's list) or a mod like it goes here, by its comp's type and its field, when the player adds it.
        /// </summary>
        public static readonly (string type, string field)[] LikenessComps = { ("RimWorld.CompStatue", "name") };

        private static readonly Dictionary<string, FieldInfo> likenessFields = new Dictionary<string, FieldInfo>();

        /// <summary>Whose likeness a statue is, by full name and short name, or null.</summary>
        public static (string full, string shortName)? Likeness(Thing t)
        {
            if (!(t is ThingWithComps tw) || tw.AllComps == null) return null;
            foreach (var c in tw.AllComps)
            {
                if (c == null) continue;
                string tn = c.GetType().FullName;
                foreach (var (type, field) in LikenessComps)
                {
                    if (tn != type) continue;
                    if (!likenessFields.TryGetValue(type, out var fi))
                    {
                        fi = AccessTools.Field(c.GetType(), field);
                        likenessFields[type] = fi;
                    }
                    var v = fi?.GetValue(c);
                    if (v is Name n) return (n.ToStringFull, n.ToStringShort);
                    if (v is string s && !s.NullOrEmpty()) return (s, s);
                }
            }
            return null;
        }

        /// <summary>Once a day: a statue of the colony's that is a colony saint's likeness, raised to no saint yet, is known for theirs.</summary>
        public static void Likenesses(Map map)
        {
            var b = Saints.Book;
            if (b == null || b.saints.Count == 0) return;
            foreach (var t in map.listerBuildings.allBuildingsColonist.ToList())
            {
                if (!IsStatue(t) || b.PlaceOf(t) != null) continue;
                var who = Likeness(t);
                if (who == null) continue;
                var s = b.saints.FirstOrDefault(x => x != null && (x.fullName == who.Value.full || x.name == who.Value.shortName));
                if (s == null) continue;
                var r = new SaintPlace { thing = t, saint = s.id, god = s.god, since = Saints.Now, byName = null };
                b.places.Add(r);
                Title(t, r, Saints.Title(s));
                Tell.Minor(NewsKind.Colony, Importance.Notable, map, "CC_Saint_Likeness".Translate(t.LabelShort.Named("PLACE"), Saints.Title(s).Named("SAINT")).Resolve(), t);
            }
        }

        // ================= The button and the inspect line =================

        internal static Gizmo GizmoFor(Thing t)
        {
            if (!Saints.On || !IsStatue(t)) return null;
            var r = Saints.Book?.PlaceOf(t);
            var v = Saints.View(r);
            string label = r == null || v == null ? "CC_Saint_Gizmo_Raise".Translate().Resolve()
                : r.pending ? "CC_Saint_Gizmo_Pending".Translate(v.title.Named("SAINT")).Resolve()
                : "CC_Saint_Gizmo_Raised".Translate(v.title.Named("SAINT")).Resolve();
            var cmd = new Command_Action
            {
                defaultLabel = label,
                defaultDesc = "CC_Saint_Gizmo_Desc".Translate().Resolve(),
                icon = t.def.uiIcon ?? BaseContent.BadTex,
                action = () => Menu(t)
            };
            if (Choices().Count == 0 && r == null) cmd.Disable("CC_Saint_Gizmo_None".Translate());
            return cmd;
        }

        private static void Menu(Thing t)
        {
            var opts = Choices().Select(c => new FloatMenuOption(c.label, () => c.raise(t))).ToList();
            if (Saints.Book?.PlaceOf(t) != null) opts.Add(new FloatMenuOption("CC_Saint_Menu_Clear".Translate(), () => Clear(t)));
            if (opts.Count > 0) Find.WindowStack.Add(new FloatMenu(opts));
        }

        internal static string InspectLine(Thing t)
        {
            if (!Saints.On) return null;
            var b = Saints.Book;
            var r = b?.PlaceOf(t);
            if (r != null)
            {
                var v = Saints.View(r);
                if (v == null) return null;
                if (r.pending) return "CC_Saint_Inspect_Pending".Translate(v.title.Named("SAINT")).Resolve();
                string line = (r.byName.NullOrEmpty() ? "CC_Saint_Inspect_Likeness" : "CC_Saint_Inspect_Statue").Translate(v.title.Named("SAINT"), (r.byName ?? "").Named("PAWN")).Resolve();
                return line + Of(v);
            }
            if (t is Building_Grave grave && grave.HasCorpse)
            {
                var s = b?.ByPawn(grave.Corpse?.InnerPawn?.ThingID);
                if (s == null) return null;
                var v = Saints.View(s, SaintPlaceKind.Grave);
                return "CC_Saint_Inspect_Grave".Translate(v.title.Named("SAINT"), v.deed.Named("DEED")).Resolve() + Of(v);
            }
            return null;
        }

        /// <summary>" Of Hollin, as their faith holds." (" Of Hollin, by the council's word." for the colony's own) where the colony knows the god, else nothing.</summary>
        internal static string Of(SaintView v)
        {
            string of = Saints.OfGod(v?.god);
            return of == null ? "" : " " + (v.colony != null ? "CC_Saint_Inspect_OfOurs" : "CC_Saint_Inspect_Of").Translate(of.Named("GOD")).Resolve();
        }
    }

    /// <summary>
    /// Praying at a saint's place, or raising a statue to a saint (Gods/Saints/SaintsPlaces.cs): walk there, a sentence, a
    /// while facing it, then the prayer said or the statue raised.
    /// </summary>
    public class JobDriver_SaintPlace : JobDriver
    {
        private bool arrived;

        private bool Raising => job.def == SaintsDefOf.Rimfolk_Saint_Raise;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override string GetReport()
        {
            var t = job.targetA.Thing;
            var v = Raising ? Saints.View(Saints.Book?.PlaceOf(t)) : Saints.ViewAt(t);
            if (v == null) return base.GetReport();
            return (Raising ? "CC_Saint_Report_Raise" : "CC_Saint_Report_Pray").Translate(v.title.Named("SAINT")).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !Saints.On || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating == StoryDanger.High);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var stay = ToilMaker.MakeToil("SaintPlace");
            stay.initAction = Arrive;
            stay.tickIntervalAction = delta => pawn.rotationTracker.FaceTarget(job.targetA);
            stay.defaultCompleteMode = ToilCompleteMode.Delay;
            stay.defaultDuration = Raising ? SaintsMath.RaiseTicks : SaintsMath.PrayTicks;
            stay.handlingFacing = true;
            stay.socialMode = RandomSocialMode.Off;
            yield return stay;
            yield return Toils_General.Do(() =>
            {
                try
                {
                    if (Raising) SaintsPlaces.Raised(pawn, job.targetA.Thing);
                    else SaintsPlaces.Prayed(pawn, job.targetA.Thing);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("saints: at a saint's place", e);
                }
            });
        }

        private void Arrive()
        {
            if (arrived) return;
            arrived = true;
            try
            {
                if (Raising) SaintsPlaces.RaiseArrive(pawn, job.targetA.Thing);
                else SaintsPlaces.PrayArrive(pawn, job.targetA.Thing);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("saints: arrived at a saint's place", e);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref arrived, "arrived");
        }
    }

    /// <summary>
    /// A relic of one of the colony's saints (Defs/GodDefs/Rimfolk_Saints.xml, Rimfolk_SaintRelic): a keepsake in the colony's
    /// age's form, named for them; a place to pray to them where it lies, and, where a believer's faith keeps relics
    /// (Ideology's relic precept), venerated as a relic (Gods/Saints/SaintsPlaces.cs, Prayed).
    /// </summary>
    public class CompSaintRelic : ThingComp
    {
        public int saint = -1;
        public int age = 1;

        private ColonySaint Saint => Saints.Book?.ById(saint);

        public override string TransformLabel(string label)
        {
            var s = Saint;
            return s == null ? label : "CC_Saint_RelicLabel".Translate(Saints.Title(s).Named("SAINT")).Resolve();
        }

        public override string CompInspectStringExtra()
        {
            var s = Saint;
            if (s == null) return null;
            var v = Saints.View(s, SaintPlaceKind.Relic);
            return "CC_Saint_RelicInspect".Translate(("CC_Saint_RelicForm_" + Mathf.Clamp(age, 0, 3)).Translate().Named("FORM"), v.title.Named("SAINT"), v.deed.Named("DEED")).Resolve()
                   + SaintsPlaces.Of(v);
        }

        public override bool AllowStackWith(Thing other) => false;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref saint, "saint", -1);
            Scribe_Values.Look(ref age, "age", 1);
        }
    }
}
